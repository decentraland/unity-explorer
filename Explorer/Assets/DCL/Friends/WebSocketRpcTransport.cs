using Cysharp.Threading.Tasks;
using rpc_csharp.transport;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Utility;

namespace DCL.Friends
{
    public class WebSocketRpcTransport : ITransport
    {
        private readonly Uri uri;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private readonly byte[] receiveBuffer;
        private readonly ClientWebSocket webSocket;

        private bool isConnected => State == WebSocketState.Open;

        public event Action? OnCloseEvent;
        public event Action<string>? OnErrorEvent;
        public event Action<byte[]>? OnMessageEvent;
        public event Action? OnConnectEvent;

        public WebSocketState State => webSocket.State;

        public WebSocketRpcTransport(Uri uri,
            int bufferSize = 100000)
        {
            this.uri = uri;
            receiveBuffer = new byte[bufferSize];
            webSocket = new ClientWebSocket();
        }

        public void Dispose()
        {
            lifeCycleCancellationToken.SafeCancelAndDispose();

            try
            {
                webSocket.Abort();
                webSocket.Dispose();
            }
            catch (ObjectDisposedException) { }
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (State is WebSocketState.Open or WebSocketState.Connecting)
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
                while (!ct.IsCancellationRequested && isConnected)
                {
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
                    catch (WebSocketException e) { OnErrorEvent?.Invoke(e.Message); }
                }
            }
        }

        public void SendMessage(byte[] data)
        {
            SendMessageAsync(data, lifeCycleCancellationToken.Token).Forget();
        }

        public async UniTask SendMessageAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
            }
            catch (WebSocketException e)
            {
                OnErrorEvent?.Invoke(e.Message);
            }
        }

        public async UniTask SendMessageAsync(string data, CancellationToken ct)
        {
            try
            {
                await webSocket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, ct);
            }
            catch (WebSocketException e)
            {
                OnErrorEvent?.Invoke(e.Message);
            }
        }

        public void Close() =>
            CloseAsync(lifeCycleCancellationToken.Token).Forget();

        public async UniTask CloseAsync(CancellationToken ct)
        {
            if (State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                OnCloseEvent?.Invoke();
            }
        }
    }
}
