// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using Xunit.Abstractions;

namespace CP.Net.Sockets.Tests;

#pragma warning disable SA1600 // Elements should be documented

public class ServerTests(ITestOutputHelper output) : SocketRxTestBase(output)
{
    [Fact]
    public void T01_Invalid_EndPoint()
    {
        IPEndPoint endPoint = new(IPAddress.Parse("111.111.111.111"), 1111);
        Assert.Throws<SocketException>(() => SocketRxServer.Create(endPoint, LogFactory));
    }

    [Fact]
    public async Task T02_Accept_Success()
    {
        var server = SocketRxServer.Create(LogFactory);
        var endPoint = server.LocalEndPoint;

        var acceptTask = server.AcceptAllAsync.FirstAsync();

        var clientSocket = Mixins.CreateSocket();
        await clientSocket.ConnectAsync(endPoint);

        var acceptedSocket = await acceptTask;

        Assert.True(clientSocket.Connected && acceptedSocket.Connected);

        await clientSocket.DisconnectAsync(false);
        await server.DisposeAsync();
    }

    [Fact]
    public async Task T03_Disconnect_Before_Accept()
    {
        var server = SocketRxServer.Create(LogFactory);
        await server.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await server.AcceptAllAsync.FirstAsync());
    }

    [Fact]
    public async Task T04_Disconnect_While_Accept()
    {
        var server = SocketRxServer.Create(LogFactory);
        var acceptTask = server.AcceptAllAsync.FirstAsync();
        await server.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await acceptTask);
    }
}
