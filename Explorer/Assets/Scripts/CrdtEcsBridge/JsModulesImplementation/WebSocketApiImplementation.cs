using Cysharp.Threading.Tasks;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using Microsoft.ClearScript;
using SceneRuntime.Apis.Modules;
using SocketIOClient;
using SocketIOClient.Messages;
using SocketIOClient.Transport;
using SocketIOClient.Transport.WebSockets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace CrdtEcsBridge.JsModulesImplementation
{
    public class WebSocketApiImplementation : IWebSocketApi
    {
        private static readonly ThreadSafeListPool<byte[]> SEND_PAYLOAD_POOL = new (1, PoolConstants.SCENES_COUNT);

        private readonly Dictionary<int, WebSocketTransport> webSockets = new ();
        private readonly ObjectPool<WebSocketTransport> webSocketPool;

        private readonly TransportOptions transportOptions = new ()
        {
            EIO = EngineIO.WebSocketDefault,
            ConnectionTimeout = TimeSpan.FromSeconds(20),
        };
        private int nextId;

        public WebSocketApiImplementation()
        {
            webSocketPool = new ObjectPool<WebSocketTransport>(() => new WebSocketTransport(transportOptions, new DefaultClientWebSocket()), null, null, w => w.Dispose(), true, 1);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<int, WebSocketTransport> valuePair in webSockets) { valuePair.Value.Dispose(); }

            webSocketPool.Dispose();
        }

        public int CreateWebSocket(string url)
        {
            nextId++;
            webSockets.Add(nextId, webSocketPool.Get());
            return nextId;
        }

        public async UniTask ConnectAsync(int websocketId, string url, CancellationToken ct)
        {
            if (webSockets.TryGetValue(websocketId, out WebSocketTransport webSocket)) { await webSocket.ConnectAsync(new Uri(url), ct); }
            else { throw new ArgumentException($"WebSocket with id {websocketId} does not exist."); }
        }

        public async UniTask SendAsync(int websocketId, object data, CancellationToken ct)
        {
            if (!webSockets.TryGetValue(websocketId, out WebSocketTransport webSocket)) { throw new ArgumentException($"WebSocket with id {websocketId} does not exist."); }

            if (data is not IScriptObject scriptObject || scriptObject.GetProperty("type") == null) { throw new ArgumentException("Invalid data format"); }

            object messageData = scriptObject.GetProperty("data");

            if (messageData != null)
            {
                var type = scriptObject.GetProperty("type").ToString();

                if (type == "Text")
                {
                    var payload = new Payload { Text = messageData.ToString() };

                    await webSocket.SendAsync(payload, ct);
                }
                else if (type == "Binary" && messageData is List<object> binaryData)
                {
                    using var payloadListScope = SEND_PAYLOAD_POOL.AutoScope();

                    var byteArray = new byte[binaryData.Count];
                    for (var i = 0; i < binaryData.Count; i++) byteArray[i] = Convert.ToByte(binaryData[i]);

                    payloadListScope.Value.Add(byteArray);

                    var payload = new Payload
                    {
                        Bytes = payloadListScope.Value,
                    };

                    await webSocket.SendAsync(payload, ct);
                }
            }
            else { throw new ArgumentException("Unsupported data type or invalid format"); }
        }

        public async UniTask CloseAsync(int websocketId, CancellationToken ct)
        {
            if (!webSockets.TryGetValue(websocketId, out WebSocketTransport webSocket)) { throw new ArgumentException($"WebSocket with id {websocketId} does not exist."); }

            await webSocket.DisconnectAsync(ct);

            webSocketPool.Release(webSocket);
        }

        public async UniTask<object> ReceiveAsync(int websocketId, CancellationToken ct)
        {
            if (!webSockets.TryGetValue(websocketId, out WebSocketTransport webSocket)) throw new ArgumentException($"WebSocket with id {websocketId} does not exist.");

            var tcs = new UniTaskCompletionSource<object>();

            void ReceivedHandler(IMessage message)
            {
                try
                {
                    object result;

                    if (message is BinaryMessage binaryMessage)
                    {
                        byte[] bytes = binaryMessage.IncomingBytes.SelectMany(x => x).ToArray();
                        result = new { type = "Binary", data = bytes };
                    }
                    else if (message is DefaultTextMessage defaultMessage) { result = new { type = "Text", data = defaultMessage.Message }; }
                    else { throw new NotSupportedException($"Unsupported message type: {message.GetType().Name}"); }

                    tcs.TrySetResult(result);
                }
                catch (Exception ex) { tcs.TrySetException(ex); }
            }

            webSocket.OnReceived += ReceivedHandler;

            await using (ct.Register(() => tcs.TrySetCanceled()))
            {
                try { return await tcs.Task; }
                finally { webSocket.OnReceived -= ReceivedHandler; }
            }
        }
    }
}
