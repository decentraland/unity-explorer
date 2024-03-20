using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using SocketIOClient;
using SocketIOClient.Messages;
using SocketIOClient.Transport;
using SocketIOClient.Transport.WebSockets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine.Pool;

namespace CrdtEcsBridge.Engine
{
    public class WebSocketApiImplementation : IWebSocketApi
    {
        private readonly Dictionary<int, WebSocketTransport> webSockets = new ();
        private readonly ObjectPool<WebSocketTransport> webSocketPool;

        private readonly TransportOptions transportOptions = new ()
        {
            EIO = EngineIO.V4,
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

            if (data is not Dictionary<string, object> dataDict || !dataDict.ContainsKey("type")) { throw new ArgumentException("Invalid data format"); }

            var type = dataDict["type"].ToString();

            if (type == "Text" && dataDict.ContainsKey("data") && dataDict["data"] is string textData)
            {
                var payload = new Payload
                    { Text = textData };

                await webSocket.SendAsync(payload, ct);
            }
            else if (type == "Binary" && dataDict.ContainsKey("data") && dataDict["data"] is List<object> binaryData)
            {
                byte[] byteArray = binaryData.Select(Convert.ToByte).ToArray();

                var payload = new Payload
                {
                    Bytes = new List<byte[]>
                        { byteArray },
                };

                await webSocket.SendAsync(payload, ct);
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
                if (message is BinaryMessage binaryMessage)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        for (var i = 0; i < binaryMessage.IncomingBytes.Count; i++)
                        {
                            byte[] byteArray = binaryMessage.IncomingBytes[i];
                            memoryStream.Write(byteArray, 0, byteArray.Length);
                        }

                        byte[] bytes = memoryStream.ToArray();
                        tcs.TrySetResult(new { type = "Binary", data = bytes });
                    }
                }
                else
                {
                    string text = message.Write();
                    tcs.TrySetResult(new { type = "Text", data = text });
                }
            }

            webSocket.OnReceived += ReceivedHandler;

            await using (ct.Register(() => tcs.TrySetCanceled())) { return await tcs.Task; }
        }
    }
}
