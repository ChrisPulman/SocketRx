// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CP.Net.Sockets;

/// <summary>
/// SocketRxServer.
/// </summary>
/// <seealso cref="ISocketRxServer" />
public sealed class SocketRxServer : ISocketRxServer
{
    private readonly CancellationTokenSource _cts = new();
    private readonly AcceptorFactory _acceptor;
    private readonly SocketDisposer _disposer;

    private SocketRxServer(Socket socket, ILogger logger)
    {
        LocalEndPoint = socket.LocalEndPoint ?? throw new InvalidOperationException();
        _acceptor = new AcceptorFactory(socket, logger);
        _disposer = new SocketDisposer(socket, _acceptor, "Server", _cts, logger);
        AcceptObservable = _acceptor.CreateAcceptObservable();
        AcceptAllAsync = _acceptor.CreateAcceptAllAsync(_cts.Token);
        logger.LogServerCreated(LocalEndPoint);
    }

    /// <summary>
    /// Gets the local end point.
    /// </summary>
    /// <value>
    /// The local end point.
    /// </value>
    public EndPoint LocalEndPoint { get; }

    /// <summary>
    /// Gets the accept observable.
    /// </summary>
    /// <value>
    /// The accept observable.
    /// </value>
    public IObservable<ISocketRxClient> AcceptObservable { get; }

    /// <summary>
    /// Gets the accept all asynchronous.
    /// </summary>
    /// <value>
    /// The accept all asynchronous.
    /// </value>
    public IAsyncEnumerable<ISocketRxClient> AcceptAllAsync { get; }

    /// <summary>
    /// Creates the specified socket.
    /// </summary>
    /// <param name="socket">The socket.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <returns>ISocketRxServer.</returns>
    /// <exception cref="System.ArgumentNullException">socket.</exception>
    public static ISocketRxServer Create(Socket socket, ILoggerFactory loggerFactory) => socket switch
    {
        null => throw new ArgumentNullException(nameof(socket)),
        _ => new SocketRxServer(socket, loggerFactory.CreateLogger<SocketRxServer>())
    };

    /// <summary>
    /// Creates the specified SocketRx.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="backLog">The back log.</param>
    /// <returns>ISocketRxServer.</returns>
    /// <exception cref="System.ArgumentNullException">endPoint.</exception>
    /// <exception cref="System.ArgumentException">Invalid backLog: {backLog}.</exception>
    public static ISocketRxServer Create(EndPoint endPoint, ILoggerFactory loggerFactory, int backLog = 10)
    {
        ArgumentNullException.ThrowIfNull(endPoint);

        // Backlog specifies the number of pending connections allowed before a busy error is returned.
        if (backLog < 0)
        {
            throw new ArgumentException($"Invalid backLog: {backLog}.");
        }

        var socket = Mixins.CreateSocket();
        socket.Bind(endPoint);
        socket.Listen(backLog);
        return Create(socket, loggerFactory);
    }

    /// <summary>
    /// Creates the specified SocketRx.
    /// </summary>
    /// <param name="backLog">The back log.</param>
    /// <returns>ISocketRxServer.</returns>
    public static ISocketRxServer Create(int backLog = 10) =>
        Create(Mixins.CreateIPEndPointOnPort(0), NullLoggerFactory.Instance, backLog);

    /// <summary>
    /// Creates the specified SocketRx.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="backLog">The back log.</param>
    /// <returns>ISocketRxServer.</returns>
    public static ISocketRxServer Create(ILoggerFactory loggerFactory, int backLog = 10) =>
        Create(Mixins.CreateIPEndPointOnPort(0), loggerFactory, backLog);

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>ValueTask.</returns>
    public async ValueTask DisposeAsync()
    {
        await _acceptor.DisposeAsync().ConfigureAwait(false);
        await _disposer.DisposeAsync().ConfigureAwait(false);
        _cts.Dispose();
    }
}
