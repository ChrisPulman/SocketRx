# SocketRx
A Reactive Socket extension of System.Net.Sockets.Socket

### example
```csharp
using System.Reactive.Linq;
using System.Linq;
using CP.Net.Sockets;
```
#### Server

```csharp
// Create a server using an available port on the local machine.
ISocketRxServer server = SocketRxServer.Create();

// Prepare to start accepting connections from clients.
server
    .AcceptObservable
    .Subscribe(onNext: acceptClient =>
    {
        // After the server accepts a client connection,
        // start receiving messages from the client and ...
        acceptClient
            .ReceiveObservable
            .ToStrings()
            .Subscribe(onNext: message =>
            {
                // Echo each message received back to the client.
                acceptClient.Send(message.ToByteArray());
            });
    });
```
#### Client

```csharp
// Create a client connected to EndPoint of the server.
ISocketRxClient client = await server.LocalEndPoint.CreateSocketRxClientAsync();

// Send the message "Hello!" to the server,
// which the server will then echo back to the client.
client.Send("Hello!".ToByteArray());

// Receive the message from the server.
string message = await client.ReceiveAllAsync.ToStrings().FirstAsync();
Assert.Equal("Hello!", message);

await client.DisposeAsync();
await server.DisposeAsync();
```
### Notes

To communicate using strings (see example above), the following extension methods are provided:
```csharp
byte[] ToByteArray(this string source);
byte[] ToByteArray(this IEnumerable<string> source)

IEnumerable<string>      ToStrings(this IEnumerable<byte> source)
IObservable<string>      ToStrings(this IObservable<byte> source)
IAsyncEnumerable<string> ToStrings(this IAsyncEnumerable<byte> source)
```
To communicate using byte arrays with a 4 byte BigEndian integer length prefix, the following extension methods are provided:
```csharp
byte[] ToByteArrayWithLengthPrefix(this byte[] source)

IEnumerable<byte[]>      ToArraysFromBytesWithLengthPrefix(this IEnumerable<byte> source)
IObservable<byte[]>      ToArraysFromBytesWithLengthPrefix(this IObservable<byte> source)
IAsyncEnumerable<byte[]> ToArraysFromBytesWithLengthPrefix(this IAsyncEnumerable<byte> source)
```
To support multiple simultaneous observers, use:
```csharp
Observable.Publish().[RefCount() | AutoConnect()] 
```
