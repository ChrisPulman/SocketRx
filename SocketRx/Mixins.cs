// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CP.Net.Sockets;

/// <summary>
/// Mixins.
/// </summary>
public static partial class Mixins
{
    /// <summary>
    /// Creates the socket rx client asynchronous.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="ct">The ct.</param>
    /// <returns>ISocketRxClient.</returns>
    public static async Task<ISocketRxClient> CreateSocketRxClientAsync(this EndPoint endPoint, CancellationToken ct = default) =>
            await CreateSocketRxClientAsync(endPoint, NullLogger.Instance, ct).ConfigureAwait(false);

    /// <summary>
    /// Creates the socket rx client asynchronous.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="factoryLogger">The factory logger.</param>
    /// <param name="ct">The ct.</param>
    /// <returns>ISocketRxClient.</returns>
    public static async Task<ISocketRxClient> CreateSocketRxClientAsync(this EndPoint endPoint, ILoggerFactory factoryLogger, CancellationToken ct = default) =>
            await CreateSocketRxClientAsync(endPoint, factoryLogger.CreateLogger<SocketRxClient>(), ct).ConfigureAwait(false);

    /// <summary>
    /// Creates the socket rx client asynchronous.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="ct">The ct.</param>
    /// <returns>ISocketRxClient.</returns>
    public static async Task<ISocketRxClient> CreateSocketRxClientAsync(this EndPoint endPoint, ILogger logger, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(endPoint);
        var socket = await ConnectAsync(endPoint, logger, ct).ConfigureAwait(false);
        return new SocketRxClient(socket, logger, "Client");
    }

    /// <summary>
    /// Creates the socket rx server.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="backLog">The back log.</param>
    /// <returns>ISocketRxServer.</returns>
    public static ISocketRxServer CreateSocketRxServer(this EndPoint endPoint, int backLog = 10) =>
        SocketRxServer.Create(endPoint, NullLoggerFactory.Instance, backLog);

    /// <summary>
    /// Creates the socket rx server.
    /// </summary>
    /// <param name="endPoint">The end point.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="backLog">The back log.</param>
    /// <returns>ISocketRxServer.</returns>
    public static ISocketRxServer CreateSocketRxServer(this EndPoint endPoint, ILoggerFactory loggerFactory, int backLog = 10) =>
        SocketRxServer.Create(endPoint, loggerFactory, backLog);

    /// <summary>
    /// Prepend a 4 byte payload length to a byte array.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArrayWithLengthPrefix(this byte[] source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var buffer = new byte[source.Length + 4];
        source.CopyTo(buffer, 4);
        EncodeMessageLength(buffer);
        return buffer;
    }

    /// <summary>
    /// Transform a sequence of bytes with a length prefix into a sequence of byte arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A byte array.</returns>
    /// <exception cref="System.IO.InvalidDataException">Invalid length: {length}.</exception>
    public static IEnumerable<byte[]> ToArraysFromBytesWithLengthPrefix(this IEnumerable<byte> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var length = -1;
        using MemoryStream ms = new();
        foreach (var b in source)
        {
            ms.WriteByte(b);
            if (length == -1 && ms.Position == 4)
            {
                length = DecodeMessageLength(ms);
                ms.SetLength(0);
            }
            else if (length == ms.Length)
            {
                yield return ms.ToArray(); // array copy
                length = -1;
                ms.SetLength(0);
            }
        }

        if (ms.Position != 0)
        {
            throw new InvalidDataException($"Invalid length: {length}.");
        }
    }

    /// <summary>
    /// Transform a sequence of bytes with a length prefix into a sequence of byte arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A byte array.</returns>
    /// <exception cref="System.IO.InvalidDataException">ToArraysFromBytesWithLengthPrefix: invalid termination.</exception>
    public static async IAsyncEnumerable<byte[]> ToArraysFromBytesWithLengthPrefix(this IAsyncEnumerable<byte> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var length = -1;
        await using MemoryStream ms = new();
        await foreach (var b in source.ConfigureAwait(false))
        {
            ms.WriteByte(b);
            if (length == -1 && ms.Position == 4)
            {
                length = DecodeMessageLength(ms);
                ms.SetLength(0);
            }
            else if (length == ms.Length)
            {
                yield return ms.ToArray(); // array copy
                length = -1;
                ms.SetLength(0);
            }
        }

        if (ms.Position != 0)
        {
            throw new InvalidDataException("ToArraysFromBytesWithLengthPrefix: invalid termination.");
        }
    }

    /// <summary>
    /// Transform a sequence of bytes with a length prefix into a sequence of byte arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>An Observable byte array.</returns>
    public static IObservable<byte[]> ToArraysFromBytesWithLengthPrefix(this IObservable<byte> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return Observable.Create<byte[]>(observer =>
        {
            var length = -1;
            MemoryStream ms = new();

            return source.Subscribe(
                onNext: b =>
                {
                    ms.WriteByte(b);
                    if (length == -1 && ms.Position == 4)
                    {
                        length = DecodeMessageLength(ms);
                        ms.SetLength(0);
                    }
                    else if (length == ms.Length)
                    {
                        observer.OnNext(ms.ToArray()); // array copy
                        length = -1;
                        ms.SetLength(0);
                    }
                },
                onError: (e) =>
                {
                    observer.OnError(e);
                    ms.Dispose();
                },
                onCompleted: () =>
                {
                    if (ms.Position == 0)
                    {
                        observer.OnCompleted();
                    }
                    else
                    {
                        observer.OnError(new InvalidDataException("ToArraysFromBytesWithLengthPrefix: incomplete."));
                    }

                    ms.Dispose();
                });
        });
    }

    /// <summary>
    /// Transform a sequence of byte arrays into a sequence of string arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string array.</returns>
    public static IEnumerable<string[]> ToStringArrays(this IEnumerable<byte[]> source) =>
        source.Select(buffer => buffer.ToStringArray());

    /// <summary>
    /// Transform a sequence of byte arrays into a sequence of string arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string array.</returns>
    public static IAsyncEnumerable<string[]> ToStringArrays(this IAsyncEnumerable<byte[]> source) =>
        source.Select(bytes => bytes.ToStringArray());

    /// <summary>
    /// Transform a sequence of byte arrays into a sequence of string arrays.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string array.</returns>
    public static IObservable<string[]> ToStringArrays(this IObservable<byte[]> source) =>
        source.Select(buffer => buffer.ToStringArray());

    /// <summary>
    /// Convert a string to a byte array.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(this string source) =>
        Encoding.UTF8.GetBytes(source + '\0');

    /// <summary>
    /// Convert a sequence of strings to a byte array.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A byte array.</returns>
    public static byte[] ToByteArray(this IEnumerable<string> source) =>
        [.. source.SelectMany(s => s.ToByteArray())];

    /// <summary>
    /// Convert a sequence of bytes into a sequence of strings.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string.</returns>
    /// <exception cref="System.IO.InvalidDataException">ToStrings: no termination(1).</exception>
    public static IEnumerable<string> ToStrings(this IEnumerable<byte> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using MemoryStream ms = new();
        foreach (var b in source)
        {
            if (b != 0)
            {
                ms.WriteByte(b);
                continue;
            }

            var s = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Position);
            ms.SetLength(0);
            yield return s;
        }

        if (ms.Position != 0)
        {
            throw new InvalidDataException("ToStrings: no termination(1).");
        }
    }

    /// <summary>
    /// Transform a sequence of bytes into a sequence of strings.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string.</returns>
    /// <exception cref="System.IO.InvalidDataException">ToStrings: invalid termination.</exception>
    public static async IAsyncEnumerable<string> ToStrings(this IAsyncEnumerable<byte> source)
    {
        await using MemoryStream ms = new();
        await foreach (var b in source.ConfigureAwait(false))
        {
            if (b != 0)
            {
                ms.WriteByte(b);
                continue;
            }

            var s = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Position);
            ms.SetLength(0);
            yield return s;
        }

        if (ms.Position != 0)
        {
            throw new InvalidDataException("ToStrings: invalid termination.");
        }
    }

    /// <summary>
    /// Convert a sequence of bytes into a sequence of strings.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A string.</returns>
    public static IObservable<string> ToStrings(this IObservable<byte> source)
    {
        MemoryStream ms = new();

        return Observable.Create<string>(observer => source.Subscribe(
                onNext: b =>
                {
                    if (b != 0)
                    {
                        ms.WriteByte(b);
                        return;
                    }

                    var s = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Position);
                    ms.SetLength(0);
                    observer.OnNext(s);
                },
                onError: (e) =>
                {
                    observer.OnError(e);
                    ms.Dispose();
                },
                onCompleted: () =>
                {
                    if (ms.Position == 0)
                    {
                        observer.OnCompleted();
                    }
                    else
                    {
                        observer.OnError(new InvalidDataException("ToStrings: invalid termination."));
                    }

                    ms.Dispose();
                }));
    }

    /// <summary>
    /// Creates the ip end point on port.
    /// </summary>
    /// <param name="port">The port.</param>
    /// <returns>IPEndPoint.</returns>
    internal static IPEndPoint CreateIPEndPointOnPort(int port) => new(IPAddress.Loopback, port);

    /// <summary>
    /// Creates the socket.
    /// </summary>
    /// <returns>Socket.</returns>
    internal static Socket CreateSocket() => new(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

    [LoggerMessage(EventId = 1, EventName = "SendBytes", Level = LogLevel.Trace, Message = "Send: {Name} on {LocalEndPoint} sending {Bytes} bytes to {RemoteEndPoint}.")]
    internal static partial void LogSend(this ILogger logger, string name, EndPoint? localEndPoint, int bytes, EndPoint? remoteEndPoint);

    [LoggerMessage(EventId = 2, EventName = "ReceiveBytes", Level = LogLevel.Trace, Message = "Receive: {Name} on {LocalEndPoint} received {Bytes} bytes from {RemoteEndPoint}.")]
    internal static partial void LogReceive(this ILogger logger, string name, EndPoint? localEndPoint, int bytes, EndPoint? remoteEndPoint);

    [LoggerMessage(EventId = 3, EventName = "ConnectionError", Level = LogLevel.Warning, Message = "Socket could not connect to {EndPoint}. {Message} {ErrorName}.")]
    internal static partial void LogConnectionError(this ILogger logger, EndPoint endPoint, string message, string errorName);

    [LoggerMessage(EventId = 4, EventName = "Connected", Level = LogLevel.Information, Message = "Client on {LocalEndPoint} connected to {EndPoint}.")]
    internal static partial void LogConnected(this ILogger logger, EndPoint? localEndPoint, EndPoint endPoint);

    [LoggerMessage(EventId = 5, EventName = "Warning", Level = LogLevel.Warning, Message = "Socket could not connect to {EndPoint}. {Message}")]
    internal static partial void LogFailedToConnect(this ILogger logger, EndPoint endPoint, string message);

    [LoggerMessage(EventId = 6, EventName = "DisposeError", Level = LogLevel.Error, Message = "DisposeAsync.")]
    internal static partial void LogDisposeError(this ILogger logger, Exception e);

    [LoggerMessage(EventId = 7, EventName = "Disposed", Level = LogLevel.Debug, Message = "{Name} on {LocalEndPoint} disposed.")]
    internal static partial void LogDisposed(this ILogger logger, string name, EndPoint? localEndPoint);

    [LoggerMessage(EventId = 8, EventName = "Disconnected", Level = LogLevel.Debug, Message = "{Name} on {LocalEndPoint} disconnected from {RemoteEndPoint} and disposed.")]
    internal static partial void LogDisconnected(this ILogger logger, string name, EndPoint? localEndPoint, EndPoint? remoteEndPoint);

    [LoggerMessage(EventId = 9, EventName = "Subscribing", Level = LogLevel.Debug, Message = "{Name}: SocketReceiverObservable Subscribing.")]
    internal static partial void LogSubscribing(this ILogger logger, string name);

    [LoggerMessage(EventId = 10, EventName = "ReceiverException", Level = LogLevel.Debug, Message = "{Name} on {LocalEndPoint} SocketReceiverObservable Exception: {Message}")]
    internal static partial void LogReceiverException(this ILogger logger, Exception e, string name, EndPoint? localEndPoint, string message);

    [LoggerMessage(EventId = 11, EventName = "ServerCreated", Level = LogLevel.Information, Message = "Server on {LocalEndPoint} created.")]
    internal static partial void LogServerCreated(this ILogger logger, EndPoint localEndPoint);

    [LoggerMessage(EventId = 12, EventName = "AcceptorError", Level = LogLevel.Error, Message = "Acceptor Error on {LocalEndPoint}. {Message}")]
    internal static partial void LogAcceptorError(this ILogger logger, EndPoint? localEndPoint, string message);

    [LoggerMessage(EventId = 13, EventName = "AcceptClient", Level = LogLevel.Debug, Message = "Accept Client on {LocalEndPoint} connected to {RemoteEndPoint}.")]
    internal static partial void LogAcceptClient(this ILogger logger, EndPoint? localEndPoint, EndPoint? remoteEndPoint);

    private static async Task<Socket> ConnectAsync(EndPoint endPoint, ILogger logger, CancellationToken ct)
    {
        var socket = CreateSocket();
        try
        {
            await socket.ConnectAsync(endPoint, ct).ConfigureAwait(false);
            logger.LogConnected(socket.LocalEndPoint, endPoint);
            return socket;
        }
        catch (Exception e)
        {
            if (e is SocketException se)
            {
                var errorName = $"SocketException: {Enum.GetName(typeof(SocketError), se.ErrorCode)}";
                logger.LogConnectionError(endPoint, e.Message, errorName);
            }
            else
            {
                logger.LogFailedToConnect(endPoint, e.Message);
            }

            throw;
        }
    }

    // Encode 4 byte BigEndian integer length prefix.
    private static void EncodeMessageLength(byte[] buffer)
    {
        var length = buffer.Length - 4;
        var i = IPAddress.HostToNetworkOrder(length);
        if (!BitConverter.TryWriteBytes(buffer, i))
        {
            throw new InvalidDataException("TryWriteBytes.");
        }
    }

    private static int DecodeMessageLength(MemoryStream ms)
    {
        var buffer = ms.GetBuffer();
        var i = BitConverter.ToInt32(buffer, 0);
        var length = IPAddress.NetworkToHostOrder(i);
        if (length <= 0)
        {
            throw new InvalidDataException($"Invalid length: {length}.");
        }

        return length;
    }

    /// <summary>
    /// Transform a byte array into an array of strings.
    /// </summary>
    private static string[] ToStringArray(this byte[] buffer)
    {
        var length = buffer.Length;
        if (length == 0 || buffer[length - 1] != 0)
        {
            throw new InvalidDataException("ToStringArray: no termination.");
        }

        return Encoding.UTF8.GetString(buffer, 0, length - 1).Split('\0');
    }
}