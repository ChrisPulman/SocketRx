// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;

namespace CP.Net.Sockets.Tests;

#pragma warning disable SA1600 // Elements should be documented
public class SocketRxServerTests
{
    [Fact]
    public void Create_WithSocket_ReturnsValidServer()
    {
        // Arrange
        var socket = Mixins.CreateSocket();
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        socket.Listen(1);
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        var server = SocketRxServer.Create(socket, loggerFactory);

        // Assert
        Assert.NotNull(server);
        Assert.NotNull(server.LocalEndPoint);
        Assert.NotNull(server.AcceptObservable);
        Assert.NotNull(server.AcceptAllAsync);
    }

    [Fact]
    public void Create_WithNullSocket_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerFactory = NullLoggerFactory.Instance;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SocketRxServer.Create(null!, loggerFactory));
    }

    [Fact]
    public void Create_WithEndPoint_ReturnsValidServer()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Loopback, 0);
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        var server = SocketRxServer.Create(endPoint, loggerFactory);

        // Assert
        Assert.NotNull(server);
        Assert.NotNull(server.LocalEndPoint);
    }

    [Fact]
    public void Create_WithNullEndPoint_ThrowsArgumentNullException()
    {
        // Arrange
        var loggerFactory = NullLoggerFactory.Instance;
        EndPoint? nullEndPoint = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SocketRxServer.Create(nullEndPoint!, loggerFactory));
    }

    [Fact]
    public void Create_WithInvalidBacklog_ThrowsArgumentException()
    {
        // Arrange
        var endPoint = new IPEndPoint(IPAddress.Loopback, 0);
        var loggerFactory = NullLoggerFactory.Instance;
        var invalidBacklog = -1;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => SocketRxServer.Create(endPoint, loggerFactory, invalidBacklog));
    }

    [Fact]
    public void Create_WithoutParameters_ReturnsValidServer()
    {
        // Act
        var server = SocketRxServer.Create();

        // Assert
        Assert.NotNull(server);
        Assert.NotNull(server.LocalEndPoint);
    }

    [Fact]
    public void Create_WithLoggerFactory_ReturnsValidServer()
    {
        // Arrange
        var loggerFactory = NullLoggerFactory.Instance;

        // Act
        var server = SocketRxServer.Create(loggerFactory);

        // Assert
        Assert.NotNull(server);
        Assert.NotNull(server.LocalEndPoint);
    }

    [Fact]
    public async Task DisposeAsync_ReleasesResources()
    {
        // Arrange
        var server = SocketRxServer.Create();

        // Act
        await server.DisposeAsync();

        // Assert - If no exception is thrown, the test passes
        // We can't check internal state directly, but successful disposal is our success criteria
    }
}

#pragma warning restore SA1600 // Elements should be documented
