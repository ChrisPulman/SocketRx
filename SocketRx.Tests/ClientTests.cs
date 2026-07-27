// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;
using Xunit.Abstractions;

namespace CP.Net.Sockets.Tests;

#pragma warning disable SA1600 // Elements should be documented

public class ClientTests(ITestOutputHelper output) : SocketRxTestBase(output)
{
    [Fact]
    public async Task T00_All_Ok()
    {
        var server = SocketRxServer.Create(LogFactory);
        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(Logger);
        await server.AcceptAllAsync.FirstAsync();
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T00_Cancellation_During_Connect()
    {
        var endPoint = TestUtilities.GetEndPointOnRandomLoopbackPort();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await endPoint.CreateSocketRxClientAsync(LogFactory, ct: new CancellationToken(true)));
    }

    [Fact]
    public async Task T00_Timeout_During_Connect()
    {
        var endPoint = TestUtilities.GetEndPointOnRandomLoopbackPort();
        await Assert.ThrowsAsync<SocketException>(async () =>
            await endPoint.CreateSocketRxClientAsync(LogFactory));
    }

    [Fact]
    public async Task T01_Dispose_Before_Receive()
    {
        var server = SocketRxServer.Create(LogFactory);
        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        await client.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await client.ReceiveAllAsync.FirstAsync());
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T02_Dispose_During_Receive()
    {
        var server = SocketRxServer.Create(LogFactory);

        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        var receiveTask = client.ReceiveAllAsync.LastOrDefaultAsync();
        await client.DisposeAsync();

        await Assert.ThrowsAsync<SocketException>(async () => await receiveTask);

        await server.DisposeAsync();
    }

    [Fact]
    public async Task T03_External_Dispose_Before_Receive()
    {
        var server = SocketRxServer.Create(LogFactory);
        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        var accept = await server.AcceptAllAsync.FirstAsync();
        await accept.DisposeAsync();
        await client.ReceiveAllAsync.LastOrDefaultAsync();
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T04_External_Dispose_During_Receive()
    {
        var server = SocketRxServer.Create(LogFactory);
        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        var accept = await server.AcceptAllAsync.FirstAsync();
        var receiveTask = client.ReceiveAllAsync.FirstAsync();
        await accept.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await receiveTask);
        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T05_Dispose_Before_Send()
    {
        var server = SocketRxServer.Create(LogFactory);
        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        await client.DisposeAsync();
        Assert.ThrowsAny<Exception>(() => client.Send([0]));
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T06_Dispose_During_Send()
    {
        var server = SocketRxServer.Create(LogFactory);

        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);
        var sendTask = Task.Run(() => client.Send(new byte[100_000_000]));
        await client.DisposeAsync();
        await Assert.ThrowsAnyAsync<Exception>(async () => await sendTask);
        await server.DisposeAsync();
    }
}
