// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;

namespace CP.Net.Sockets;

/// <summary>
/// ISocketRxClient.
/// </summary>
/// <seealso cref="IAsyncDisposable" />
public interface ISocketRxClient : IAsyncDisposable
{
    /// <summary>
    /// Gets the remote end point.
    /// </summary>
    /// <value>
    /// The remote end point.
    /// </value>
    EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets a value indicating whether this <see cref="ISocketRxClient"/> is connected.
    /// </summary>
    /// <value>
    ///   <c>true</c> if connected; otherwise, <c>false</c>.
    /// </value>
    bool Connected { get; }

    /// <summary>
    /// Gets the receive observable.
    /// </summary>
    /// <value>
    /// The receive observable.
    /// </value>
    IObservable<byte> ReceiveObservable { get; }

    /// <summary>
    /// Gets the receive all asynchronous.
    /// </summary>
    /// <value>
    /// The receive all asynchronous.
    /// </value>
    IAsyncEnumerable<byte> ReceiveAllAsync { get; }

    /// <summary>
    /// Sends the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <returns>An int.</returns>
    int Send(ReadOnlySpan<byte> buffer);
}
