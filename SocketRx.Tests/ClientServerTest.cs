// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Reactive.Linq;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace CP.Net.Sockets.Tests;

#pragma warning disable SA1600 // Elements should be documented
public class ClientServerTest(ITestOutputHelper output) : SocketRxTestBase(output)
{
    [Fact]
    public async Task CreateWith_HandshakeAsync()
    {
        var server = SocketRxServer.Create(LogFactory);

        server.AcceptObservable
            .Select(acceptClient => Observable.FromAsync(async ct =>
            {
                var message1 = await acceptClient.ReceiveAllAsync.ToStrings().FirstAsync(ct);
                Assert.Equal("Hello1FromClient", message1);

                acceptClient.Send(new[] { "Hello1FromServer" }.ToByteArray());

                var messages = await acceptClient.ReceiveAllAsync.ToArraysFromBytesWithLengthPrefix().ToStringArrays().FirstAsync(ct);
                Assert.Equal("Hello2FromClient", messages[0]);

                acceptClient.Send(new[] { "Hello2FromServer" }.ToByteArray().ToByteArrayWithLengthPrefix());

                acceptClient.Send(new[] { "Hello3FromServer" }.ToByteArray().ToByteArrayWithLengthPrefix());
            }))
            .Concat()
            .Subscribe();

        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);

        // Send the first message without prefix.
        client.Send("Hello1FromClient".ToByteArray());

        // Receive the response message without prefix.
        var message1 = await client.ReceiveAllAsync.ToStrings().FirstAsync();
        Assert.Equal("Hello1FromServer", message1);

        // Start sending and receiving messages with an int32 message length prefix.
        client.Send(new[] { "Hello2FromClient" }.ToByteArray().ToByteArrayWithLengthPrefix());

        var message3 = await client.ReceiveAllAsync.ToArraysFromBytesWithLengthPrefix().ToStringArrays().FirstAsync();
        Assert.Equal("Hello2FromServer", message3.Single());

        client.ReceiveObservable
            .ToArraysFromBytesWithLengthPrefix()
            .ToStringArrays()
            .Subscribe(x =>
            {
                Logger.LogInformation(x[0]);
            });

        await Task.Delay(10);

        await client.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task CreateWith_HandshakeObservable()
    {
        var server = SocketRxServer.Create(LogFactory);

        server.AcceptObservable
            .Select(acceptClient => Observable.FromAsync(async ct =>
            {
                var message1 = await acceptClient.ReceiveAllAsync.ToStrings().FirstAsync(ct);
                Assert.Equal("Hello1FromClient", message1);

                acceptClient.Send(new[] { "Hello1FromServer" }.ToByteArray());

                var messages = await acceptClient.ReceiveAllAsync.ToArraysFromBytesWithLengthPrefix().ToStringArrays().FirstAsync(ct);
                Assert.Equal("Hello2FromClient", messages[0]);

                acceptClient.Send(new[] { "Hello2FromServer" }.ToByteArray().ToByteArrayWithLengthPrefix());

                acceptClient.Send(new[] { "Hello3FromServer" }.ToByteArray().ToByteArrayWithLengthPrefix());
            }))
            .Concat()
            .Subscribe();

        var client = await server.LocalEndPoint.CreateSocketRxClientAsync(LogFactory);

        // Send the first message without prefix.
        client.Send("Hello1FromClient".ToByteArray());

        // Receive the response message without prefix.
        var message1 = await client.ReceiveAllAsync.ToStrings().FirstAsync();
        Assert.Equal("Hello1FromServer", message1);

        // Start sending and receiving messages with an int32 message length prefix.
        client.Send(new[] { "Hello2FromClient" }.ToByteArray().ToByteArrayWithLengthPrefix());

        var message3 = await client.ReceiveAllAsync.ToArraysFromBytesWithLengthPrefix().ToStringArrays().FirstAsync();
        Assert.Equal("Hello2FromServer", message3.Single());

        client.ReceiveObservable
            .ToArraysFromBytesWithLengthPrefix()
            .ToStringArrays()
            .Subscribe(x =>
            {
                Debug.Assert(Thread.CurrentThread.IsBackground, "Not a background thread.");
                Logger.LogInformation("xxx");
            });

        await Task.Delay(10);

        await client.DisposeAsync();
        await server.DisposeAsync();
    }
}
#pragma warning restore SA1600 // Elements should be documented
