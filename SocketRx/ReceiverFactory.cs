// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace CP.Net.Sockets;

/// <summary>
/// ReceiverFactory.
/// </summary>
internal sealed class ReceiverFactory
{
    private readonly ILogger _logger;
    private readonly Socket _socket;
    private readonly string _name;
    private readonly byte[] _buffer = new byte[0x10000];
    private int _position;
    private int _bytesReceived;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReceiverFactory"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="name">The name.</param>
    /// <param name="logger">The logger.</param>
    internal ReceiverFactory(Socket socket, string name, ILogger logger)
    {
        _socket = socket;
        _name = name;
        _logger = logger;
    }

    /// <summary>
    /// Creates the receive observable.
    /// </summary>
    /// <returns>A byte.</returns>
    internal IObservable<byte> CreateReceiveObservable()
    {
        Debug.Assert(Thread.CurrentThread.IsBackground, "Not a background thread.");

        return Observable.Create<byte>(async (observer, ct) =>
        {
            _logger.LogSubscribing(_name);

            Debug.Assert(Thread.CurrentThread.IsBackground, "Not a background thread.");
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_position == _bytesReceived)
                    {
                        _bytesReceived = await _socket.ReceiveAsync(_buffer, ct).ConfigureAwait(false);
                        _position = 0;

                        if (_bytesReceived == 0)
                        {
                            observer.OnCompleted();
                            return;
                        }

                        _logger.LogReceive(_name, _socket.LocalEndPoint, _bytesReceived, _socket.RemoteEndPoint);
                    }

                    Debug.Assert(Thread.CurrentThread.IsBackground, "Not a background thread.");

                    observer.OnNext(_buffer[_position++]);
                }
            }
            catch (Exception ex)
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                _logger.LogReceiverException(ex, _name, _socket.LocalEndPoint, ex.Message);
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Receives all asynchronous.
    /// </summary>
    /// <param name="ct">The ct.</param>
    /// <returns>A byte.</returns>
    internal async IAsyncEnumerable<byte> ReceiveAllAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_position == _bytesReceived)
            {
                try
                {
                    _bytesReceived = await _socket.ReceiveAsync(_buffer, ct).ConfigureAwait(false);
                    _position = 0;
                }
                catch (Exception)
                {
                    if (ct.IsCancellationRequested)
                    {
                        yield break;
                    }

                    throw;
                }

                if (_bytesReceived == 0)
                {
                    yield break;
                }

                _logger.LogReceive(_name, _socket.LocalEndPoint, _bytesReceived, _socket.RemoteEndPoint);
            }

            yield return _buffer[_position++];
        }
    }
}
