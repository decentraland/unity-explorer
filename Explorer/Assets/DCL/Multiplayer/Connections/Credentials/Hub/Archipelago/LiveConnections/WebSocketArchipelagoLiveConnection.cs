using Cysharp.Threading.Tasks;
using LiveKit.Internal.FFIClients.Pools.Memory;
using SocketIOClient.Transport;
using SocketIOClient.Transport.WebSockets;
using System;
using System.Buffers;
using System.Threading;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public class WebSocketArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        private readonly IClientWebSocket webSocket;
        private readonly IMemoryPool memoryPool;
        private const int BUFFER_SIZE = 1024 * 1024; //1MB

        public WebSocketArchipelagoLiveConnection() : this(
            new DefaultClientWebSocket(),
            new ArrayMemoryPool(ArrayPool<byte>.Shared!)
        ) { }

        public WebSocketArchipelagoLiveConnection(IClientWebSocket webSocket, IMemoryPool memoryPool)
        {
            this.webSocket = webSocket;
            this.memoryPool = memoryPool;
        }

        public bool Connected() =>
            webSocket.State is WebSocketState.Open;

        public UniTask ConnectAsync(string adapterUrl, CancellationToken token) =>
            webSocket.ConnectAsync(new Uri(adapterUrl), token)!.AsUniTask();

        public UniTask DisconnectAsync(CancellationToken token) =>
            webSocket.DisconnectAsync(token)!.AsUniTask();

        public async UniTask SendAsync(MemoryWrap data, CancellationToken token)
        {
            using (data)
            {
                byte[] buffer = data.DangerousBuffer();

                await webSocket.SendAsync(
                    buffer,
                    TransportMessageType.Binary,
                    true,
                    token
                )!;
            }
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            using var result = await webSocket.ReceiveAsync(BUFFER_SIZE, token)!;
            EnsureBinary(result.MessageType);
            return CopiedMemory(result.Buffer, result.Count);
        }

        private MemoryWrap CopiedMemory(ReadOnlyMemory<byte> buffer, int count)
        {
            var memory = memoryPool.Memory(count);
            var slice = buffer.Slice(0, count).Span;
            slice.CopyTo(memory.Span());
            return memory;
        }

        private static void EnsureBinary(TransportMessageType type)
        {
            if (type != TransportMessageType.Binary)
                throw new NotSupportedException("Only binary messages are supported");
        }
    }
}
