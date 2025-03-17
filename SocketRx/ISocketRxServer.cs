// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;

namespace CP.Net.Sockets;

/// <summary>
/// ISocketRxServer.
/// </summary>
/// <seealso cref="System.IAsyncDisposable" />
public interface ISocketRxServer : IAsyncDisposable
{
    /// <summary>
    /// Gets the local end point.
    /// </summary>
    /// <value>
    /// The local end point.
    /// </value>
    EndPoint LocalEndPoint { get; }

    /// <summary>
    /// Gets the accept observable.
    /// </summary>
    /// <value>
    /// The accept observable.
    /// </value>
    IObservable<ISocketRxClient> AcceptObservable { get; }

    /// <summary>
    /// Gets the accept all asynchronous.
    /// </summary>
    /// <value>
    /// The accept all asynchronous.
    /// </value>
    IAsyncEnumerable<ISocketRxClient> AcceptAllAsync { get; }
}
