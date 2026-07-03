using System;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace SocketIOClient.Transport.WebSockets
{
    public interface IClientWebSocket : IDisposable
    {
        WebSocketState State { get; }

        UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken);

        UniTask DisconnectAsync(CancellationToken cancellationToken);

        UniTask SendAsync(ReadOnlyMemory<byte> bytes, TransportMessageType type, bool endOfMessage, CancellationToken cancellationToken);

        UniTask<WebSocketReceiveResult> ReceiveAsync(int bufferSize, CancellationToken cancellationToken);
    }
}
