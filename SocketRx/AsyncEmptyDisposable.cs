// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;

namespace CP.Net.Sockets;

/// <summary>
/// AsyncEmptyDisposable.
/// </summary>
/// <seealso cref="IAsyncDisposable" />
public sealed class AsyncEmptyDisposable : IAsyncDisposable
{
    /// <summary>
    /// Prevents a default instance of the <see cref="AsyncEmptyDisposable"/> class from being created.
    /// </summary>
    private AsyncEmptyDisposable()
    {
    }

    /// <summary>
    /// Gets the instance.
    /// </summary>
    /// <value>
    /// The instance.
    /// </value>
    public static IAsyncDisposable Instance { get; } = new AsyncEmptyDisposable();

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or
    /// resetting unmanaged resources asynchronously.
    /// </summary>
    /// <returns>ValueTask.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
