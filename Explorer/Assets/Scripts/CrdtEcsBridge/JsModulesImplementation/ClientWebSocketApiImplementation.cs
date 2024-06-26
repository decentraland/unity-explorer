using CrdtEcsBridge.PoolsProviders;
using Cysharp.Threading.Tasks;
using Microsoft.ClearScript.JavaScript;
using SceneRuntime;
using SceneRuntime.Apis.Modules;
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrdtEcsBridge.JsModulesImplementation
{
    /// <summary>
    /// Uses raw .NET ClientWebSocket under the hood
    /// </summary>
    public class ClientWebSocketApiImplementation : IWebSocketApi
    {
        private static readonly ChunkTransmission CHUNK_TRANSMISSION = new ();

        private readonly IInstancePoolsProvider instancePoolsProvider;
        private readonly IJsOperations jsOperations;

        private readonly ConcurrentDictionary<int, WebSocketRental> webSockets = new ();

        private int nextId;

        public ClientWebSocketApiImplementation(IInstancePoolsProvider instancePoolsProvider, IJsOperations jsOperations)
        {
            this.instancePoolsProvider = instancePoolsProvider;
            this.jsOperations = jsOperations;
        }

        public void Dispose()
        {
            foreach (WebSocketRental rental in webSockets.Values)
                rental.Dispose();

            webSockets.Clear();
        }

        public int CreateWebSocket(string url)
        {
            Interlocked.Increment(ref nextId);

            webSockets[nextId] = new WebSocketRental(); // ClientWebSocket does not support reviving
            return nextId;
        }

        public async UniTask ConnectAsync(int websocketId, string url, CancellationToken ct)
        {
            await GetInstanceOrThrow(websocketId).WebSocket.ConnectAsync(new Uri(url), ct);
        }

        public async UniTask SendBinaryAsync(int websocketId, IArrayBuffer data, CancellationToken ct)
        {
            WebSocketRental webSocket = GetInstanceOrThrow(websocketId);

            var bytesCount = (int)data.Size;
            if (bytesCount == 0) return;

            using PoolableByteArray poolableArray = instancePoolsProvider.GetAPIRawDataPool(bytesCount);

            data.ReadBytes(0, data.Size, poolableArray.Array, 0);
            await CHUNK_TRANSMISSION.SendAsync(webSocket, poolableArray.Memory, WebSocketMessageType.Binary, ct);
        }

        public async UniTask SendTextAsync(int websocketId, string data, CancellationToken ct)
        {
            WebSocketRental webSocket = GetInstanceOrThrow(websocketId);

            int utfBytesCount = Encoding.UTF8.GetByteCount(data);

            if (utfBytesCount == 0) return;

            using PoolableByteArray poolableArray = instancePoolsProvider.GetAPIRawDataPool(utfBytesCount);

            Encoding.UTF8.GetBytes(data, poolableArray.Memory.Span);
            await CHUNK_TRANSMISSION.SendAsync(webSocket, poolableArray.Memory, WebSocketMessageType.Text, ct);
        }

        public async UniTask CloseAsync(int websocketId, CancellationToken ct)
        {
            if (!webSockets.TryGetValue(websocketId, out WebSocketRental webSocket))
                throw new ArgumentException($"WebSocket with id {websocketId} does not exist.");

            await webSocket.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, ct);
        }

        public async UniTask<IWebSocketApi.ReceiveResponse> ReceiveAsync(int websocketId, CancellationToken ct)
        {
            WebSocketRental webSocket = GetInstanceOrThrow(websocketId);

            (PoolableByteArray result, WebSocketMessageType messageType, WebSocketCloseStatus closeStatus)
                = await CHUNK_TRANSMISSION.ReceiveAsync(webSocket.WebSocket, instancePoolsProvider, ct);

            // by creating a JS array here we can free the result array immediately
            using (result)
            {
                if (closeStatus != WebSocketCloseStatus.Empty)
                    throw new WebSocketException((int)closeStatus, $"WebSocket with id {websocketId} is already closed");

                return new IWebSocketApi.ReceiveResponse
                {
                    type = messageType == WebSocketMessageType.Text ? "Text" : "Binary",
                    data = jsOperations.CreateUint8Array(result.Memory),
                };
            }
        }

        public IWebSocketApi.JSWebSocketState GetState(int webSocketId)
        {
            WebSocketRental dotNetState = GetInstanceOrThrow(webSocketId);

            switch (dotNetState.WebSocket.State)
            {
                case WebSocketState.Aborted:
                case WebSocketState.Closed:
                    return IWebSocketApi.JSWebSocketState.CLOSED;
                case WebSocketState.Connecting:
                    return IWebSocketApi.JSWebSocketState.CONNECTING;
                case WebSocketState.CloseSent:
                case WebSocketState.CloseReceived:
                    return IWebSocketApi.JSWebSocketState.CLOSING;
                case WebSocketState.Open:
                    return IWebSocketApi.JSWebSocketState.OPEN;
                default:
                    throw new WebSocketException($"Unknown WebSocket state: {dotNetState.WebSocket.State}");
            }
        }

        private WebSocketRental GetInstanceOrThrow(int websocketId)
        {
            // TODO add meaningful exception handling on the wrapper level

            if (!webSockets.TryGetValue(websocketId, out WebSocketRental webSocket))
                throw new ArgumentException($"WebSocket with id {websocketId} does not exist.");

            return webSocket;
        }

        /// <summary>
        ///     Handles Sending and Receiving in chunks, tightly coupled with WebSocket APIs
        /// </summary>
        private class ChunkTransmission
        {
            internal const int SIZE8_K = 8 * 1024;

            private readonly int receiveChunkSize;
            private readonly int sendChunkSize;

            public ChunkTransmission(int receiveChunkSize = SIZE8_K, int sendChunkSize = SIZE8_K)
            {
                this.receiveChunkSize = receiveChunkSize;
                this.sendChunkSize = sendChunkSize;
            }

            public async ValueTask SendAsync(WebSocketRental rental, ReadOnlyMemory<byte> data, WebSocketMessageType messageType, CancellationToken ct)
            {
                try
                {
                    await rental.SendLock.WaitAsync(ct).ConfigureAwait(false);

                    var pages = (int)Math.Ceiling(data.Length * 1.0 / sendChunkSize);

                    for (var i = 0; i < pages; i++)
                    {
                        int offset = i * sendChunkSize;
                        int length = sendChunkSize;

                        if (offset + length > data.Length) length = data.Length - offset;

                        ReadOnlyMemory<byte> subBuffer = data.Slice(offset, length);
                        bool endOfMessage = pages - 1 == i;
                        await rental.WebSocket.SendAsync(subBuffer, messageType, endOfMessage, ct).ConfigureAwait(false);
                    }
                }
                finally { rental.SendLock.Release(); }
            }

            public async Task<(PoolableByteArray result, WebSocketMessageType messageType, WebSocketCloseStatus closeStatus)> ReceiveAsync(ClientWebSocket webSocket, IInstancePoolsProvider instancePoolsProvider, CancellationToken ct)
            {
                PoolableByteArray finalBuffer = PoolableByteArray.EMPTY;

                try
                {
                    using PoolableByteArray chunkBuffer = instancePoolsProvider.GetAPIRawDataPool(receiveChunkSize);

                    WebSocketMessageType messageType;

                    while (true)
                    {
                        WebSocketReceiveResult? chunkResult = await webSocket.ReceiveAsync(chunkBuffer.Array, ct).ConfigureAwait(false);

                        if (chunkResult.CloseStatus != null && chunkResult.CloseStatus != WebSocketCloseStatus.Empty)
                            return (finalBuffer, WebSocketMessageType.Close, chunkResult.CloseStatus!.Value);

                        int oldLength = finalBuffer.Length;

                        finalBuffer = instancePoolsProvider.Expand(finalBuffer, oldLength + chunkResult.Count);

                        // copy new data starting from the oldLength
                        Array.Copy(chunkBuffer.Array, 0, finalBuffer.Array, oldLength, chunkResult.Count);

                        messageType = chunkResult.MessageType;

                        if (chunkResult.EndOfMessage)
                            break;
                    }

                    return (finalBuffer, messageType, WebSocketCloseStatus.Empty);
                }
                catch (Exception)
                {
                    // if exception occurs we need to dispose the buffer here, otherwise it's returned to the upper layer
                    finalBuffer.Dispose();
                    throw;
                }
            }
        }

        private class WebSocketRental : IDisposable
        {
            public readonly SemaphoreSlim SendLock = new (1, 1);
            public readonly ClientWebSocket WebSocket = new ();

            public void Dispose()
            {
                WebSocket.Dispose();
                SendLock.Dispose();
            }
        }
    }
}
