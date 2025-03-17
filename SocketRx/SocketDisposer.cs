// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CP.Net.Sockets;

internal sealed class SocketDisposer : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Socket _socket;
    private readonly IAsyncDisposable _disposable;
    private readonly string _name;
    private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenSource _receiveCts;
    private int _disposals;

    internal SocketDisposer(Socket socket, string name, CancellationTokenSource receiveCts, ILogger logger)
        : this(socket, AsyncEmptyDisposable.Instance, name, receiveCts, logger)
    {
    }

    internal SocketDisposer(Socket socket, IAsyncDisposable disposable, string name, CancellationTokenSource receiveCts, ILogger logger)
    {
        _socket = socket;
        _receiveCts = receiveCts;
        _logger = logger;
        _name = name;
        _disposable = disposable;
    }

    internal bool DisposeRequested => _disposals > 0;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Increment(ref _disposals) > 1)
        {
            await _tcs.Task.ConfigureAwait(false);
            return;
        }

        try
        {
            await _receiveCts.CancelAsync().ConfigureAwait(false);

            var localEndPoint = _socket.LocalEndPoint;
            var remoteEndPoint = _socket.RemoteEndPoint;

            if (_socket.Connected)
            {
                // disables Send method and queues up a zero-byte send packet in the send buffer
                _socket.Shutdown(SocketShutdown.Send);
                await _socket.DisconnectAsync(false).ConfigureAwait(false);
                _logger.LogDisconnected(_name, localEndPoint, remoteEndPoint);
            }
            else
            {
                _logger.LogDisposed(_name, localEndPoint);
            }

            // SocketAcceptor or AsyncEmptyDisposable
            await _disposable.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogDisposeError(e);
        }
        finally
        {
            _tcs.SetResult(true);
            _socket.Dispose();
            _receiveCts.Dispose();
        }
    }
}
