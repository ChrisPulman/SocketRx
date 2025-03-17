// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CP.Net.Sockets.Tests;

#pragma warning disable SA1600 // Elements should be documented
public abstract class SocketRxTestBase
{

    protected SocketRxTestBase(ITestOutputHelper output)
    {
        ArgumentNullException.ThrowIfNull(output);

        Write = output.WriteLine;

        LogFactory = LoggerFactory.Create(builder => builder
            .AddMXLogger(Write)
            .SetMinimumLevel(LogLevel.Debug));

        Logger = LogFactory.CreateLogger("Test");
    }

    protected Action<string> Write { get; }

    protected ILoggerFactory LogFactory { get; }

    protected ILogger Logger { get; }
}

#pragma warning restore SA1600 // Elements should be documented
