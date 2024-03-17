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
            // Create a task completion source to await the event
            var tcs = new UniTaskCompletionSource<string>();

            // Define the event handler for the OnReceived event
            Action<IMessage> receivedHandler = null;
            receivedHandler = (message) =>
            {
                // Unsubscribe the event handler
                webSocket.Transport.OnReceived -= receivedHandler;
                string messageContent = message.Write();
                // Set the result of the task completion source
                tcs.TrySetResult(messageContent);
            };

            webSocket.Transport.OnReceived += receivedHandler;

            await using (ct.Register(() => tcs.TrySetCanceled()))
            {
                return await tcs.Task;
            }
        }

        public void Dispose() { }

    }
}
