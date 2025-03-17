// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CP.Net.Sockets;

internal sealed class AcceptorFactory : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Socket _socket;
    private readonly List<ISocketRxClient> _clients = [];

    internal AcceptorFactory(Socket socket, ILogger logger)
    {
        _socket = socket;
        _logger = logger;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>ValueTask.</returns>
    public async ValueTask DisposeAsync()
    {
        var tasks = _clients.ConvertAll(client => client.DisposeAsync().AsTask());
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    internal IObservable<ISocketRxClient> CreateAcceptObservable() =>
        Observable.Create<ISocketRxClient>(async (observer, ct) =>
        {
            Debug.Assert(Thread.CurrentThread.IsBackground, "Not a background thread.");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var acceptSocket = await _socket.AcceptAsync(ct).ConfigureAwait(false);
                    observer.OnNext(CreateClient(acceptSocket));
                }
                catch (Exception e)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    _logger.LogAcceptorError(_socket.LocalEndPoint, e.Message);
                    observer.OnError(e);
                    return;
                }
            }
        });

    internal async IAsyncEnumerable<ISocketRxClient> CreateAcceptAllAsync([EnumeratorCancellation] CancellationToken ct)
    {
        Socket acceptSocket;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                acceptSocket = await _socket.AcceptAsync(ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (ct.IsCancellationRequested)
                {
                    yield break;
                }

                _logger.LogAcceptorError(_socket.LocalEndPoint, e.Message);
                throw;
            }

            yield return CreateClient(acceptSocket);
        }
    }

    private SocketRxClient CreateClient(Socket acceptSocket)
    {
        _logger.LogAcceptClient(_socket.LocalEndPoint, acceptSocket.RemoteEndPoint);
        SocketRxClient client = new(acceptSocket, _logger, "AcceptClient");
        _clients.Add(client);
        return client;
    }
}
