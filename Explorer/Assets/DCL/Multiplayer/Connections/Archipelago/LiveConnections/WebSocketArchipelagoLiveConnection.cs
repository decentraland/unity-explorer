using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Typing;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Utility.Multithreading;
using Utility.Ownership;

namespace DCL.Multiplayer.Connections.Archipelago.LiveConnections
{
    public class WebSocketArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        private const int BUFFER_SIZE = 1024 * 1024; //1MB
        private readonly ClientWebSocket webSocket;
        private readonly IMemoryPool memoryPool;
        private readonly Atomic<bool> isSomeoneReceiving = new (false);

        public bool IsConnected => webSocket.State is WebSocketState.Open;

        public WebSocketArchipelagoLiveConnection() : this(
            new ClientWebSocket(),
            new ArrayMemoryPool(ArrayPool<byte>.Shared!)
        ) { }

        public WebSocketArchipelagoLiveConnection(ClientWebSocket webSocket, IMemoryPool memoryPool)
        {
            this.webSocket = webSocket;
            this.memoryPool = memoryPool;
        }

        ~WebSocketArchipelagoLiveConnection()
        {
            webSocket.Dispose();
        }

        public UniTask ConnectAsync(string adapterUrl, CancellationToken token) =>
            webSocket.ConnectAsync(new Uri(adapterUrl), token)!.AsUniTask(false);

        public UniTask DisconnectAsync(CancellationToken token) =>
            webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token)!.AsUniTask();

        public async UniTask SendAsync(MemoryWrap data, CancellationToken token)
        {
            using (data)
                await webSocket.SendAsync(data.DangerousArraySegment(), WebSocketMessageType.Binary, true, token)!;
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            using var ownership = new AtomicUniqueOwnership(
                isSomeoneReceiving,
                "Someone is already receiving data, cannot handle 2 data receivers at the same time"
            );

            using MemoryWrap memory = memoryPool.Memory(BUFFER_SIZE);
            byte[] buffer = memory.DangerousBuffer();
            WebSocketReceiveResult? result = await webSocket.ReceiveAsync(buffer, token)!;

            return result.MessageType switch
                   {
                       WebSocketMessageType.Text => throw new NotSupportedException(
                           $"Expected Binary, Text messages are not supported: {AsText(result, buffer)}"
                       ),
                       WebSocketMessageType.Binary => CopiedMemory(buffer, result.Count),
                       WebSocketMessageType.Close => throw new ConnectionClosedException(webSocket),
                       _ => throw new ArgumentOutOfRangeException(),
                   };
        }

        private static string AsText(WebSocketReceiveResult result, byte[] buffer)
        {
            if (result.MessageType is not WebSocketMessageType.Text)
                throw new NotSupportedException(
                    $"Expected Text, {result.MessageType} messages are not supported to converting to text"
                );

            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }

        private MemoryWrap CopiedMemory(ReadOnlyMemory<byte> buffer, int count)
        {
            MemoryWrap memory = memoryPool.Memory(count);
            ReadOnlySpan<byte> slice = buffer.Slice(0, count).Span;
            slice.CopyTo(memory.Span());
            return memory;
        }
    }
}
