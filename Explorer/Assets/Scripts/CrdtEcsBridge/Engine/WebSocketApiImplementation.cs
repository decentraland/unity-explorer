using Cysharp.Threading.Tasks;
using SceneRuntime.Apis.Modules;
using SocketIOClient;
using SocketIOClient.Messages;
using SocketIOClient.Transport;
using System;
using System.Threading;

namespace CrdtEcsBridge.Engine
{
    public class WebSocketApiImplementation : IWebSocketApi
    {
        private SocketIO webSocket;

        public void Dispose()
        {
            webSocket.Dispose();
        }

        public void CreateWebSocket(string url)
        {
            var uri = new Uri(url);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            await webSocket.ConnectAsync();
        }

        public async UniTask SendAsync(string data, CancellationToken ct)
        {
            await webSocket.EmitAsync("Send", ct, data);
        }

        public async UniTask CloseAsync(CancellationToken ct)
        {
            await webSocket.DisconnectAsync();
        }

        public async UniTask<string> ReceiveAsync(CancellationToken ct)
        {
            var tcs = new UniTaskCompletionSource<string>();

            void ReceivedHandler(IMessage message)
            {
                webSocket.Transport.OnReceived -= ReceivedHandler;
                string messageContent = message.Write();
                tcs.TrySetResult(messageContent);
            }

            webSocket.Transport.OnReceived += ReceivedHandler;

            await using (ct.Register(() => tcs.TrySetCanceled())) { return await tcs.Task; }
        }
    }
}
