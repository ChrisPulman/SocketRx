﻿namespace CP.Net.Sockets
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
        private IDisposable disposable;
        private bool initComplete = false;
        private bool? isAvailable = null;
        private bool? isConnected = null;
        private bool portDisposed = true;
        private Socket socket;
        private ISubject<Exception> socketExceptionSubject = new Subject<Exception>();

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketRx"/> class.
        /// </summary>
        /// <param name="socketType">Type of the socket.</param>
        /// <param name="protocolType">Type of the protocol.</param>
        public SocketRx(SocketType socketType, ProtocolType protocolType)
        {
            this.SocketType = socketType;
            this.ProtocolType = protocolType;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SocketRx"/> class.
        /// </summary>
        /// <param name="addressFamily">The address family.</param>
        /// <param name="socketType">Type of the socket.</param>
        /// <param name="protocolType">Type of the protocol.</param>
        public SocketRx(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
        {
            this.AddressFamily = addressFamily;
            this.SocketType = socketType;
            this.ProtocolType = protocolType;
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
                isAvailable = null;
                int count = 0;
                return Observable.Timer(TimeSpan.Zero, TimeSpan.FromSeconds(1)).Subscribe(_ =>
                {
                    count++;
                    if (isAvailable == null || !this.isAvailable.HasValue || (count == 1 && !isAvailable.Value) || (count == 10 && isAvailable.Value))
                    {
                        count = 0;
                        using (var ping = new Ping())
                        {
                            if (this.IPAddress != null)
                            {
                                isAvailable = false;
                                obs.OnError(new ArgumentNullException("IP"));
                            }
                            else
                            {
                                try
                                {
                                    PingReply result;
                                    result = ping.Send(this.IPAddress);
                                    isAvailable = result?.Status == IPStatus.Success;
                                }
                                catch (PingException)
                                {
                                    isAvailable = false;
                                }
                            }
                        }
                    }
                    var isAvail = this.isAvailable != null && this.isAvailable.HasValue ? this.isAvailable.Value : false;
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
                this.isConnected = null;
                return Observable.Interval(TimeSpan.FromMilliseconds(100)).Subscribe(_ =>
                {
                    if (socket == null)
                    {
                        this.isConnected = false;
                    }
                    else
                    {
                        try
                        {
                            this.isConnected = socket.Connected || (socket.Poll(1000, SelectMode.SelectRead) && socket.Available == 0);
                        }
                        catch (Exception)
                        {
                            this.isConnected = false;
                        }
                    }
                    var isCon = this.isConnected != null && this.isConnected.HasValue ? this.isConnected.Value : false;

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
        public int ReceiveTimeout { get; set; } = 0;

        /// <summary>
        /// Gets or sets the send timeout.
        /// </summary>
        /// <value>The send timeout.</value>
        public int SendTimeout { get; set; } = 0;

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
            disposable?.Dispose();
            this.portDisposed = true;
        }

        /// <summary>
        /// Connects the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        public void Connect(IPAddress address, int port)
        {
            if (!this.portDisposed)
            {
                this.portDisposed = false;
                this.IPAddress = address;
                this.Port = port;
                var endpoint = new IPEndPoint(this.IPAddress, this.Port);
                Task.Run(() => disposable = this.CreatePort(endpoint).Subscribe());
                return;
            }
            socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Connects the specified address.
        /// </summary>
        /// <param name="address">The address.</param>
        /// <param name="port">The port.</param>
        public void Connect(string address, int port)
        {
            if (!this.portDisposed)
            {
                this.portDisposed = false;
                this.IPAddress = IPAddress.Parse(address);
                this.Port = port;
                var endpoint = new IPEndPoint(this.IPAddress, this.Port);
                Task.Run(() => disposable = this.CreatePort(endpoint).Subscribe());
                return;
            }
            socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Connects the specified endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint.</param>
        public void Connect(IPEndPoint endpoint)
        {
            if (!this.portDisposed)
            {
                this.portDisposed = false;
                this.IPAddress = endpoint.Address;
                this.Port = endpoint.Port;
                Task.Run(() => disposable = this.CreatePort(endpoint).Subscribe());
                return;
            }
            socketExceptionSubject.OnNext(new Exception("Socket already connected"));
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting
        /// unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            this.disposable?.Dispose();
            this.socket?.Dispose();
            ((IDisposable)socketExceptionSubject)?.Dispose();
            this.portDisposed = false;
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
            if (this.initComplete)
            {
                try
                {
                    if (socket?.Connected == true)
                    {
                        var r = (int)socket?.Receive(buffer, size, socketFlags);
                        return r;
                    }
                    socketExceptionSubject.OnNext(new Exception("Device not connected"));
                }
                catch (Exception ex)
                {
                    socketExceptionSubject.OnNext(ex);
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
            if (this.initComplete)
            {
                try
                {
                    if (socket?.Connected == true)
                    {
                        var r = (int)socket?.Send(buffer, size, socketFlags);
                        return r;
                    }
                    socketExceptionSubject.OnNext(new Exception("Device not connected"));
                }
                catch (Exception ex)
                {
                    socketExceptionSubject.OnNext(ex);
                }
            }

            return -1;
        }

        private void CloseSocket(Socket socket)
        {
            if (socket != null && socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
                socket = null;
            }
        }

        private IObservable<bool> CreatePort(IPEndPoint endpoint) =>
                                                            Observable.Create<bool>(obs =>
            {
                var dis = new CompositeDisposable();
                this.socket = new Socket(this.AddressFamily, this.SocketType, this.ProtocolType);
                dis.Add(socket);
                this.initComplete = false;

                dis.Add(socketExceptionSubject.Subscribe(ex =>
                {
                    if (ex != null)
                    {
                        obs.OnError(ex);
                    }
                }));
                dis.Add(IsConnected.Subscribe(deviceConnected =>
                {
                    var isAvail = this.isAvailable != null && this.isAvailable.HasValue ? this.isAvailable.Value : false;
                    obs.OnNext(isAvail && deviceConnected);
                    if (this.initComplete && !deviceConnected)
                    {
                        CloseSocket(socket);
                        obs.OnError(new Exception("Device not connected"));
                        return;
                    }
                }, ex =>
                {
                    CloseSocket(socket);
                    obs.OnError(ex);
                }));
                dis.Add(IsAvailable.Subscribe(deviceAvailiable =>
                {
                    try
                    {
                        if (this.isAvailable != null)
                        {
                            var isAvail = this.isAvailable != null && this.isAvailable.HasValue ? this.isAvailable.Value : false;
                            if (isAvail)
                            {
                                if (!this.initComplete)
                                {
                                    var socketCreated = false;
                                    if (this.socket == null)
                                    {
                                        socketCreated = false;
                                    }
                                    else
                                    {
                                        this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, this.ReceiveTimeout);
                                        this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, this.SendTimeout);
                                        this.socket.Connect(endpoint);
                                        this.isConnected = this.socket.Connected || (this.socket.Poll(1000, SelectMode.SelectRead) && this.socket.Available == 0);
                                        socketCreated = this.isConnected != null && this.isConnected.HasValue ? this.isConnected.Value : false;
                                    }
                                    bool deviceInitialised = this.InitialiseDevice();
                                    this.initComplete = socketCreated && deviceInitialised;
                                    if (!socketCreated)
                                    {
                                        CloseSocket(socket);
                                        obs.OnError(new Exception("Device not connected"));
                                        return;
                                    }
                                }
                                var isCon = this.isConnected != null && this.isConnected.HasValue ? this.isConnected.Value : false;
                                if (this.initComplete && !isCon)
                                {
                                    CloseSocket(socket);
                                    obs.OnError(new Exception("Device not connected"));
                                    return;
                                }
                            }
                            else
                            {
                                CloseSocket(socket);
                                obs.OnError(new Exception("Device Unavailable"));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        CloseSocket(socket);
                        obs.OnError(ex);
                    }
                }, ex =>
                {
                    CloseSocket(socket);
                    obs.OnError(ex);
                }));

                return dis;
            }).Retry().Publish(false).RefCount();
    }
}