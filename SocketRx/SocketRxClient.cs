// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CP.Net.Sockets;

/// <summary>
/// SocketRxClient.
/// </summary>
/// <seealso cref="ISocketRxClient" />
public sealed class SocketRxClient : ISocketRxClient
{
    private readonly string _name;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _receiveCts = new();
    private readonly Socket _socket;
    private readonly SocketDisposer _disposer;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketRxClient"/> class.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="name">The name.</param>
    internal SocketRxClient(Socket socket, ILogger logger, string name)
    {
        _socket = socket;
        _logger = logger;
        _name = name;
        RemoteEndPoint = _socket.RemoteEndPoint ?? throw new InvalidOperationException();
        _disposer = new SocketDisposer(socket, _name, _receiveCts, _logger);
        ReceiverFactory receiver = new(socket, _name, _logger);
        ReceiveObservable = receiver.CreateReceiveObservable();
        ReceiveAllAsync = receiver.ReceiveAllAsync();
    }

    /// <summary>
    /// Gets the remote end point.
    /// </summary>
    /// <value>
    /// The remote end point.
    /// </value>
    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets a value indicating whether this <see cref="ISocketRxClient" /> is connected.
    /// </summary>
    /// <value>
    ///   <c>true</c> if connected; otherwise, <c>false</c>.
    /// </value>
    public bool Connected =>
        !((_socket.Poll(1000, SelectMode.SelectRead) && _socket.Available == 0) || !_socket.Connected);

    /// <summary>
    /// Gets the receive observable.
    /// </summary>
    /// <value>
    /// The receive observable.
    /// </value>
    public IObservable<byte> ReceiveObservable { get; }

    /// <summary>
    /// Gets the receive all asynchronous.
    /// </summary>
    /// <value>
    /// The receive all asynchronous.
    /// </value>
    public IAsyncEnumerable<byte> ReceiveAllAsync { get; }

    /// <summary>
    /// Sends the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>
    /// An int.
    /// </returns>
    public int Send(ReadOnlySpan<byte> buffer)
    {
        _logger.LogSend(_name, _socket.LocalEndPoint, buffer.Length, _socket.RemoteEndPoint);
        return _socket.Send(buffer);
    }

    /// <summary>
    /// Disposes the asynchronous.
    /// </summary>
    /// <returns>ValueTask.</returns>
    public async ValueTask DisposeAsync()
    {
        await _disposer.DisposeAsync().ConfigureAwait(false);
        _receiveCts.Dispose();
    }
}
