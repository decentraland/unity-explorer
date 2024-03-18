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

namespace CrdtEcsBridge.Engine
{
    public class WebSocketApiImplementation : IWebSocketApi
    {
        private WebSocketTransport  webSocket;
        private readonly TransportOptions transportOptions = new TransportOptions()
        {
            EIO = EngineIO.V4,
            ConnectionTimeout = TimeSpan.FromSeconds(20),
        };


        public void Dispose()
        {
            webSocket.Dispose();
        }

        public void CreateWebSocket(string url)
        {
            webSocket = new WebSocketTransport(transportOptions, new DefaultClientWebSocket());
        }

        public async UniTask ConnectAsync(string url, CancellationToken ct)
        {
            await webSocket.ConnectAsync(new Uri(url), ct);
        }

        public async UniTask SendAsync(object data, CancellationToken ct)
        {
            if (data is Dictionary<string, object> dataDict && dataDict.ContainsKey("type"))
            {
                var type = dataDict["type"].ToString();

                if (type == "Text" && dataDict.ContainsKey("data") && dataDict["data"] is string textData)
                {
                    var payload = new Payload() { Text = textData };
                    await webSocket.SendAsync(payload, ct);
                }
                else if (type == "Binary" && dataDict.ContainsKey("data") && dataDict["data"] is List<object> binaryData)
                {
                    byte[] byteArray = binaryData.Select(Convert.ToByte).ToArray();
                    var payload = new Payload() { Bytes = new List<byte[]>() {byteArray} };
                    await webSocket.SendAsync(payload, ct);
                }
                else
                {
                    throw new ArgumentException("Unsupported data type or invalid format");
                }
            }
            else
            {
                throw new ArgumentException("Invalid data format");
            }
        }

        public async UniTask CloseAsync(CancellationToken ct)
        {
            await webSocket.DisconnectAsync(ct);
        }

        public async UniTask<object> ReceiveAsync(CancellationToken ct)
        {
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
