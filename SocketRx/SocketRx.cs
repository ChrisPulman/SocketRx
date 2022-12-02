namespace CP.Net.Sockets
{
    using System;
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Reactive.Disposables;
    using System.Reactive.Linq;
    using System.Reactive.Subjects;
    using System.Threading.Tasks;

    /// <summary>
    /// Socket Rx
    /// </summary>
    /// <seealso cref="System.IDisposable"/>
    public class SocketRx : IDisposable
    {
        private IDisposable _disposable;
        private bool _initComplete;
        private bool? _isAvailable;
        private bool? _isConnected;
        private bool _portDisposed = true;
        private Socket _socket;
        private readonly ISubject<Exception> _socketExceptionSubject = new Subject<Exception>();
        private bool _disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketRx"/> class.
        /// </summary>
        /// <param name="socketType">Type of the socket.</param>
        /// <param name="protocolType">Type of the protocol.</param>
        public SocketRx(SocketType socketType, ProtocolType protocolType)
        {
            SocketType = socketType;
            ProtocolType = protocolType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketRx"/> class.
        /// </summary>
        /// <param name="addressFamily">The address family.</param>
        /// <param name="socketType">Type of the socket.</param>
        /// <param name="protocolType">Type of the protocol.</param>
        public SocketRx(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            AddressFamily = addressFamily;
            SocketType = socketType;
            ProtocolType = protocolType;
        }

        /// <summary>
        /// Gets the address family.
        /// </summary>
        /// <value>The address family.</value>
        public AddressFamily AddressFamily { get; } = AddressFamily.InterNetwork;

        /// <summary>
        /// Gets the ip address.
        /// </summary>
        /// <value>The ip address.</value>
        public IPAddress IPAddress { get; private set; }

        /// <summary>
        /// Gets the is available.
        /// </summary>
        /// <value>The is available.</value>
        public IObservable<bool> IsAvailable =>
                    Observable.Create<bool>(obs =>
                    {
                        _isAvailable = null;
                        int count = 0;
                        return Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(_ =>
                        {
                            count++;
                            if (_isAvailable == null || !_isAvailable.HasValue || (count == 1 && !_isAvailable.Value) || (count == 10 && _isAvailable.Value))
                            {
                                count = 0;
                                using (var ping = new Ping())
                                {
                                    if (IPAddress != null)
                                    {
                                        _isAvailable = false;
                                        obs.OnError(new ArgumentNullException("IP"));
                                    }
                                    else
                                    {
                                        try
                                        {
                                            var result = ping.Send(IPAddress);
                                            if (result != null)
                                            {
                                                _isAvailable = result?.Status == IPStatus.Success;
                                            }
                                        }
                                        catch (PingException)
                                        {
                                            _isAvailable = false;
                                        }
                                    }
                                }
                            }
                            var isAvail = _isAvailable != null && _isAvailable.HasValue ? _isAvailable.Value : false;
                            obs.OnNext(isAvail);
                        });
                    }).Retry().Publish(false).RefCount();

        /// <summary>
        /// Gets the is connected.
        /// </summary>
        /// <value>The is connected.</value>
        public IObservable<bool> IsConnected =>
                            Observable.Create<bool>(obs =>
                            {
                                _isConnected = null;
                                return Observable.Interval(TimeSpan.FromMilliseconds(100)).Subscribe(_ =>
                                {
                                    if (_socket == null)
                                    {
                                        _isConnected = false;
                                    }
                                    else
                                    {
                                        try
                                        {
                                            _isConnected = _socket.Connected || (_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0);
                                        }
                                        catch (Exception)
                                        {
                                            _isConnected = false;
                                        }
                                    }
                                    var isCon = _isConnected != null && _isConnected.HasValue ? _isConnected.Value : false;

                                    obs.OnNext(isCon);
                                });
                            }).Retry().Publish(false).RefCount();

        /// <summary>
        /// Gets the port.
        /// </summary>
        /// <value>The port.</value>
        public int Port { get; private set; }

        /// <summary>
        /// Gets the type of the protocol.
        /// </summary>
        /// <value>The type of the protocol.</value>
        public ProtocolType ProtocolType { get; } = ProtocolType.Tcp;

        /// <summary>
        /// Gets or sets the receive timeout.
        /// </summary>
        /// <value>The receive timeout.</value>
        public int ReceiveTimeout { get; set; }

        /// <summary>
        /// Gets or sets the send timeout.
        /// </summary>
        /// <value>The send timeout.</value>
        public int SendTimeout { get; set; }

        /// <summary>
        /// Gets the type of the socket.
        /// </summary>
        /// <value>The type of the socket.</value>
        public SocketType SocketType { get; } = SocketType.Stream;

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public void Close()
        {
            _disposable?.Dispose();
            _portDisposed = true;
        }

        /// <summary>
        /// Connects the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        public void Connect(IPAddress address, int port)
        {
            if (!_portDisposed)
            {
                _portDisposed = false;
                IPAddress = address;
                Port = port;
                var endpoint = new IPEndPoint(IPAddress, Port);
                Task.Run(() => _disposable = CreatePort(endpoint).Subscribe());
                return;
            }
            _socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Connects the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        public void Connect(string address, int port)
        {
            if (!_portDisposed)
            {
                _portDisposed = false;
                IPAddress = IPAddress.Parse(address);
                Port = port;
                var endpoint = new IPEndPoint(IPAddress, Port);
                Task.Run(() => _disposable = CreatePort(endpoint).Subscribe());
                return;
            }
            _socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Connects the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        public void Connect(IPEndPoint endpoint)
        {
            if (!_portDisposed && endpoint != null)
            {
                _portDisposed = false;
                IPAddress = endpoint.Address;
                Port = endpoint.Port;
                Task.Run(() => _disposable = CreatePort(endpoint).Subscribe());
                return;
            }
            _socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Initializes the connected device after connecting.
        /// </summary>
        /// <returns></returns>
        public virtual bool InitialiseDevice() => true;

        /// <summary>
        /// Receives the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="size">The size.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns></returns>
        public int Receive(byte[] buffer, int size, SocketFlags socketFlags = SocketFlags.None)
        {
            if (_initComplete)
            {
                try
                {
                    if (_socket?.Connected == true)
                    {
                        return (int)_socket?.Receive(buffer, size, socketFlags)!;
                    }
                    _socketExceptionSubject.OnNext(new Exception("Device not connected"));
                }
                catch (Exception ex)
                {
                    _socketExceptionSubject.OnNext(ex);
                }
            }

            return -1;
        }

        /// <summary>
        /// Sends the specified buffer.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="size">The size.</param>
        /// <param name="socketFlags">The socket flags.</param>
        /// <returns></returns>
        public int Send(byte[] buffer, int size, SocketFlags socketFlags = SocketFlags.None)
        {
            if (_initComplete)
            {
                try
                {
                    if (_socket?.Connected == true)
                    {
                        return (int)_socket?.Send(buffer, size, socketFlags)!;
                    }
                    _socketExceptionSubject.OnNext(new Exception("Device not connected"));
                }
                catch (Exception ex)
                {
                    _socketExceptionSubject.OnNext(ex);
                }
            }

            return -1;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private static void CloseSocket(Socket socket)
        {
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
            }
        }

        private IObservable<bool> CreatePort(IPEndPoint endpoint) =>
                                                            Observable.Create<bool>(obs =>
                                                            {
                                                                var dis = new CompositeDisposable();
                                                                _socket = new Socket(AddressFamily, SocketType, ProtocolType);
                                                                dis.Add(_socket);
                                                                _initComplete = false;

                                                                dis.Add(_socketExceptionSubject.Subscribe(ex =>
                                                                {
                                                                    if (ex != null)
                                                                    {
                                                                        obs.OnError(ex);
                                                                    }
                                                                }));
                                                                dis.Add(IsConnected.Subscribe(deviceConnected =>
                                                                {
                                                                    var isAvail = _isAvailable != null && _isAvailable.HasValue ? _isAvailable.Value : false;
                                                                    obs.OnNext(isAvail && deviceConnected);
                                                                    if (_initComplete && !deviceConnected)
                                                                    {
                                                                        SocketRx.CloseSocket(_socket);
                                                                        obs.OnError(new Exception("Device not connected"));
                                                                        return;
                                                                    }
                                                                }, ex =>
                                                                {
                                                                    SocketRx.CloseSocket(_socket);
                                                                    obs.OnError(ex);
                                                                }));
                                                                dis.Add(IsAvailable.Subscribe(deviceAvailiable =>
                                                                {
                                                                    try
                                                                    {
                                                                        if (_isAvailable != null)
                                                                        {
                                                                            var isAvail = _isAvailable != null && _isAvailable.HasValue ? _isAvailable.Value : false;
                                                                            if (isAvail)
                                                                            {
                                                                                if (!_initComplete)
                                                                                {
                                                                                    var socketCreated = false;
                                                                                    if (_socket == null)
                                                                                    {
                                                                                        socketCreated = false;
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, this.ReceiveTimeout);
                                                                                        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, this.SendTimeout);
                                                                                        _socket.Connect(endpoint);
                                                                                        _isConnected = _socket.Connected || (_socket.Poll(1000, SelectMode.SelectRead) && this._socket.Available == 0);
                                                                                        socketCreated = _isConnected != null && _isConnected.HasValue ? _isConnected.Value : false;
                                                                                    }
                                                                                    var deviceInitialised = InitialiseDevice();
                                                                                    _initComplete = socketCreated && deviceInitialised;
                                                                                    if (!socketCreated)
                                                                                    {
                                                                                        SocketRx.CloseSocket(_socket);
                                                                                        obs.OnError(new Exception("Device not connected"));
                                                                                        return;
                                                                                    }
                                                                                }
                                                                                var isCon = _isConnected != null && _isConnected.HasValue ? _isConnected.Value : false;
                                                                                if (_initComplete && !isCon)
                                                                                {
                                                                                    SocketRx.CloseSocket(_socket);
                                                                                    obs.OnError(new Exception("Device not connected"));
                                                                                    return;
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                SocketRx.CloseSocket(_socket);
                                                                                obs.OnError(new Exception("Device Unavailable"));
                                                                            }
                                                                        }
                                                                    }
                                                                    catch (Exception ex)
                                                                    {
                                                                        SocketRx.CloseSocket(_socket);
                                                                        obs.OnError(ex);
                                                                    }
                                                                }, ex =>
                                                                {
                                                                    SocketRx.CloseSocket(_socket);
                                                                    obs.OnError(ex);
                                                                }));

                                                                return dis;
                                                            }).Retry().Publish(false).RefCount();

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _disposable?.Dispose();
                    _socket?.Dispose();
                    ((IDisposable)_socketExceptionSubject)?.Dispose();
                    _portDisposed = false;
                }

                _disposedValue = true;
            }
        }
    }
}
