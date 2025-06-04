using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Utility;
using ITransport = rpc_csharp.transport.ITransport;

namespace DCL.SocialService
{
    public class WebSocketRpcTransport : ITransport
    {
        private readonly Uri uri;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private readonly byte[] receiveBuffer;
        private readonly ClientWebSocket webSocket;

        private bool isConnected => State == WebSocketState.Open;

        public event Action? OnCloseEvent;
        public event Action<Exception> OnErrorEvent;
        public event Action<byte[]> OnMessageEvent;
        public event Action OnConnectEvent;

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
                            if (!string.IsNullOrEmpty(result.CloseStatusDescription))
                                ReportHub.LogError(ReportCategory.FRIENDS, $"Friends web socket disconnected. {result.CloseStatusDescription}");

                            await CloseAsync(ct);
                            break;
                        }
                    }
                    catch (WebSocketException e) { OnErrorEvent?.Invoke(e); }
                }
            }
        }

        public void SendMessage(byte[] data)
        {
            CancellationToken ct;

            // The cancellation source could be disposed before the token is obtained.
            try { ct = lifeCycleCancellationToken.Token; }
            catch (ObjectDisposedException) { return; }

            SendMessageAsync(data, ct).Forget();
        }

        public async UniTask SendMessageAsync(byte[] data, CancellationToken ct)
        {
            try
            {
                await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
            }
            catch (WebSocketException e)
            {
                OnErrorEvent?.Invoke(e);
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
                OnErrorEvent?.Invoke(e);
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
