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
        private readonly byte[] receiveBuffer;

        private bool isConnected => webSocket.State == WebSocketState.Open;

        public event Action? OnCloseEvent;
        public event Action<string>? OnErrorEvent;
        public event Action<byte[]>? OnMessageEvent;
        public event Action? OnConnectEvent;

        public WebSocketState State => webSocket.State;

        public WebSocketRpcTransport(Uri uri,
            int bufferSize = 4096)
        {
            this.uri = uri;
            webSocket = new ClientWebSocket();
            receiveBuffer = new byte[bufferSize];
        }

        public void Dispose()
        {
            lifeCycleCancellationToken.SafeCancelAndDispose();
            webSocket.Dispose();
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (webSocket.State is WebSocketState.Open)
                throw new Exception("Web socket already connected");

            await webSocket.ConnectAsync(uri, ct);
            OnConnectEvent?.Invoke();
        }

        public void ListenForIncomingData()
        {
            if (!isConnected)
                throw new Exception("Web socket not connected");

            ListenAndProcessIncomingDataAsync(lifeCycleCancellationToken.Token).Forget();
            return;

            async UniTaskVoid ListenAndProcessIncomingDataAsync(CancellationToken ct)
            {
                while (!ct.IsCancellationRequested)
                {
                    if (!isConnected)
                        break;

                    try
                    {
                        WebSocketReceiveResult result = await webSocket.ReceiveAsync(receiveBuffer, ct);

                        if (result.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                        {
                            var data = new byte[result.Count];
                            receiveBuffer.AsSpan(0, result.Count).CopyTo(data);
                            // Buffer.BlockCopy(receiveBuffer, 0, data, 0, result.Count);
                            OnMessageEvent?.Invoke(data);
                        }

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await CloseAsync(ct);
                            break;
                        }
                    }
                    catch (WebSocketException e)
                    {
                        OnErrorEvent?.Invoke(e.Message);
                    }
                }
            }
        }

        public void SendMessage(byte[] data)
        {
            async UniTaskVoid SendMessageAsync(CancellationToken ct)
            {
                try { await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct); }
                catch (WebSocketException e) { OnErrorEvent?.Invoke(e.Message); }
            }

            SendMessageAsync(lifeCycleCancellationToken.Token).Forget();
        }

        public void Close() =>
            CloseAsync(lifeCycleCancellationToken.Token).Forget();

        private async UniTask CloseAsync(CancellationToken ct)
        {
            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
            OnCloseEvent?.Invoke();
        }
    }
}
