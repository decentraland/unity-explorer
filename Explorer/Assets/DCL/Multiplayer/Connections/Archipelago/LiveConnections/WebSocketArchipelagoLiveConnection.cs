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

        private readonly Func<ClientWebSocket> webSocketFactory;
        private readonly IMemoryPool memoryPool;

        private Current? current;

        public bool IsConnected => current?.WebSocket.State is WebSocketState.Open;

        public WebSocketArchipelagoLiveConnection() : this(
            () => new ClientWebSocket(),
            new ArrayMemoryPool(ArrayPool<byte>.Shared!)
        ) { }

        public WebSocketArchipelagoLiveConnection(Func<ClientWebSocket> webSocketFactory, IMemoryPool memoryPool)
        {
            this.webSocketFactory = webSocketFactory;
            this.memoryPool = memoryPool;
            current = Current.New(webSocketFactory);
        }

        public async UniTask ConnectAsync(string adapterUrl, CancellationToken token)
        {
            TryUpdateWebSocket();

            try { await current!.Value.WebSocket.ConnectAsync(new Uri(adapterUrl), token).AsUniTask(false); }
            catch (Exception e) { throw new Exception($"Cannot connect to adapter url: {adapterUrl}", e); }
        }

        public UniTask DisconnectAsync(CancellationToken token)
        {
            TryUpdateWebSocket();
            return current!.Value.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token)!.AsUniTask();
        }

        public async UniTask SendAsync(MemoryWrap data, CancellationToken token)
        {
            if (IsWebSocketInvalid())
                throw new InvalidOperationException(
                    $"Cannot send data, ensure that connection is correct, the connection is invalid: {current?.WebSocket.State}"
                );

            using (data)
                await current!.Value.WebSocket.SendAsync(data.DangerousArraySegment(), WebSocketMessageType.Binary, true, token)!;
        }

        public async UniTask<MemoryWrap> ReceiveAsync(CancellationToken token)
        {
            if (IsWebSocketInvalid())
                throw new InvalidOperationException(
                    $"Cannot receive data, ensure that connection is correct, the connection is invalid: {current?.WebSocket.State}"
                );

            using var ownership = new AtomicUniqueOwnership(
                current!.Value.IsSomeoneReceiving,
                "Someone is already receiving data, cannot handle 2 data receivers at the same time"
            );

            using MemoryWrap memory = memoryPool.Memory(BUFFER_SIZE);
            byte[] buffer = memory.DangerousBuffer();
            WebSocketReceiveResult? result = await current!.Value.WebSocket.ReceiveAsync(buffer, token)!;

            return result.MessageType switch
                   {
                       WebSocketMessageType.Text => throw new NotSupportedException(
                           $"Expected Binary, Text messages are not supported: {AsText(result, buffer)}"
                       ),
                       WebSocketMessageType.Binary => CopiedMemory(buffer, result.Count),
                       WebSocketMessageType.Close => throw new ConnectionClosedException(current!.Value.WebSocket),
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

        private void TryUpdateWebSocket()
        {
            if (IsWebSocketInvalid())
            {
                current?.Dispose();
                current = Current.New(webSocketFactory);
            }
        }

        private bool IsWebSocketInvalid() =>
            current?.WebSocket is not { State: WebSocketState.Open };

        private readonly struct Current : IDisposable
        {
            public readonly ClientWebSocket WebSocket;
            public readonly Atomic<bool> IsSomeoneReceiving;

            private Current(ClientWebSocket webSocket, Atomic<bool> isSomeoneReceiving)
            {
                this.WebSocket = webSocket;
                this.IsSomeoneReceiving = isSomeoneReceiving;
            }

            public static Current New(Func<ClientWebSocket> factory) =>
                new (factory()!, new Atomic<bool>(false));

            public void Dispose()
            {
                WebSocket.Dispose();
            }
        }
    }
}
