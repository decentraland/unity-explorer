using Cysharp.Threading.Tasks;
using rpc_csharp.transport;
using System;
using System.Net.WebSockets;
using System.Threading;
using Utility;

namespace DCL.Friends
{
    public class WebSocketRpcTransport : ITransport
    {
        private readonly Uri uri;
        private readonly ClientWebSocket webSocket;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private readonly byte[] receiveBuffer = new byte[4096];

        public event Action? OnCloseEvent;
        public event Action<string>? OnErrorEvent;
        public event Action<byte[]>? OnMessageEvent;
        public event Action? OnConnectEvent;

        public WebSocketState State => webSocket.State;

        public WebSocketRpcTransport(Uri uri)
        {
            this.uri = uri;
            webSocket = new ClientWebSocket();
        }

        public void Dispose()
        {
            lifeCycleCancellationToken.SafeCancelAndDispose();
            webSocket.Dispose();
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            await webSocket.ConnectAsync(uri, ct);
            OnConnectEvent?.Invoke();
        }

        public void ListenForIncomingData()
        {
            async UniTaskVoid StartReceivingAsync(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested && webSocket.State == WebSocketState.Open)
                {
                    try
                    {
                        var result = await webSocket.ReceiveAsync(receiveBuffer, ct);

                        // TODO: use count, length ?
                        OnMessageEvent?.Invoke(receiveBuffer);
                    }
                    catch (Exception e) { OnErrorEvent?.Invoke(e.Message); }
                }
            }

            StartReceivingAsync(lifeCycleCancellationToken.Token).Forget();
        }

        public void SendMessage(byte[] data)
        {
            async UniTaskVoid SendMessageAsync(CancellationToken ct)
            {
                try { await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct); }
                catch (Exception e) { OnErrorEvent?.Invoke(e.Message); }
            }

            SendMessageAsync(lifeCycleCancellationToken.Token).Forget();
        }

        public void Close()
        {
            async UniTaskVoid CloseAsync(CancellationToken ct)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.Empty, "", ct);
                OnCloseEvent?.Invoke();
            }

            CloseAsync(lifeCycleCancellationToken.Token).Forget();
        }
    }
}
