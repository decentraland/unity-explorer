using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Typing;
using DCL.Utility.Types;
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
    /// <summary>
    ///     Pure transport implementation: doesn't contain recovery logic
    /// </summary>
    public class WebSocketArchipelagoLiveConnection : IArchipelagoLiveConnection
    {
        private const int BUFFER_SIZE = 1024 * 1024; //1MB

        private readonly IMemoryPool memoryPool;

        private Current? current;

        public bool IsConnected => current?.WebSocket.State is WebSocketState.Open or WebSocketState.Connecting;

        public WebSocketArchipelagoLiveConnection(IMemoryPool memoryPool)
        {
            this.memoryPool = memoryPool;
            current = Current.New();
        }

        public async UniTask<Result> ConnectAsync(string adapterUrl, CancellationToken token)
        {
            TryUpdateWebSocket();

            try
            {
                await current!.Value.WebSocket.ConnectAsync(new Uri(adapterUrl), token).AsUniTask(false);
                return Result.SuccessResult();
            }
            catch (Exception e) { return Result.ErrorResult($"Cannot connect to adapter url: {adapterUrl}, {e.Message}"); }
        }

        public async UniTask<Result> DisconnectAsync(CancellationToken token)
        {
            try
            {
                TryUpdateWebSocket();
                await current!.Value.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, token)!.AsUniTask();
                return Result.SuccessResult();
            }
            catch (Exception e) { return Result.ErrorResult($"Cannot disconnect: {e}"); }
        }

        public async UniTask<EnumResult<IArchipelagoLiveConnection.ResponseError>> SendAsync(MemoryWrap data, CancellationToken token)
        {
            await WaitWhileConnectingAsync(token);

            if (IsWebSocketInvalid())
                return EnumResult<IArchipelagoLiveConnection.ResponseError>
                   .ErrorResult(
                        IArchipelagoLiveConnection.ResponseError.ConnectionClosed,
                        $"Cannot send data, ensure that connection is correct, the connection is invalid: {current?.WebSocket.State}"
                    );

            try
            {
                using (data)
                    await current!.Value.WebSocket.SendAsync(data.DangerousArraySegment(), WebSocketMessageType.Binary, true, token)!;

                return EnumResult<IArchipelagoLiveConnection.ResponseError>.SuccessResult();
            }
            catch (Exception e)
            {
                return EnumResult<IArchipelagoLiveConnection.ResponseError>
                   .ErrorResult(
                        IArchipelagoLiveConnection.ResponseError.ConnectionClosed,
                        $"Cannot send data, {e.Message}"
                    );
            }
        }

        public async UniTask<EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>> ReceiveAsync(CancellationToken token)
        {
            await WaitWhileConnectingAsync(token);

            if (IsWebSocketInvalid())
                return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                    IArchipelagoLiveConnection.ResponseError.ConnectionClosed,
                    $"Cannot receive data, ensure that connection is correct, the connection is invalid: {current?.WebSocket.State}"
                );

            using var ownership = new AtomicUniqueOwnership(
                current!.Value.IsSomeoneReceiving,
                "Someone is already receiving data, cannot handle 2 data receivers at the same time"
            );

            using MemoryWrap memory = memoryPool.Memory(BUFFER_SIZE);
            byte[] buffer = memory.DangerousBuffer();

            try
            {
                WebSocketReceiveResult? result = await current!.Value.WebSocket.ReceiveAsync(buffer, token)!;

                return result.MessageType switch
                       {
                           WebSocketMessageType.Text => EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(IArchipelagoLiveConnection.ResponseError.MessageError, $"Expected Binary, Text messages are not supported: {AsText(result, buffer)}"),
                           WebSocketMessageType.Binary => EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.SuccessResult(CopiedMemory(buffer, result.Count)),
                           WebSocketMessageType.Close => ConnectionClosedException.NewErrorResult(current!.Value.WebSocket),
                           _ => EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                               IArchipelagoLiveConnection.ResponseError.MessageError,
                               $"Unknown message type: {result.MessageType}"
                           ),
                       };
            }
            catch (Exception e)
            {
                return EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError>.ErrorResult(
                    IArchipelagoLiveConnection.ResponseError.MessageError,
                    $"Cannot receive data, {e}"
                );
            }
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

        /// <summary>
        ///     <see cref="ClientWebSocket" /> throws <see cref="NullReferenceException" /> if the state is <see cref="WebSocketState.Connecting" />
        /// </summary>
        private async UniTask WaitWhileConnectingAsync(CancellationToken ct)
        {
            while (current?.WebSocket.State is WebSocketState.Connecting)
                await UniTask.Yield(ct);
        }

        private void TryUpdateWebSocket()
        {
            if (!IsConnected) // if connection is being established such socket is considered as valid
            {
                current?.Dispose();
                current = Current.New();
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
                WebSocket = webSocket;
                IsSomeoneReceiving = isSomeoneReceiving;
            }

            public static Current New() =>
                new (new ClientWebSocket(), new Atomic<bool>(false));

            public void Dispose()
            {
                WebSocket.Dispose();
            }
        }
    }
}
