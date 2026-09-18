using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Text;
using System.Threading;
using Utility;
using ITransport = rpc_csharp.transport.ITransport;
using Utility.Networking;

namespace DCL.SocialService
{
    public class WebSocketRpcTransport : ITransport
    {
        private readonly Uri uri;
        private readonly CancellationTokenSource lifeCycleCancellationToken = new ();
        private readonly byte[] receiveBuffer;
        private readonly DCLWebSocket webSocket;

        private bool isDisposed;

        private bool isConnected => State == WebSocketState.Open;

        public event Action? OnCloseEvent;
        public event Action<Exception>? OnErrorEvent;
        public event Action<byte[]>? OnMessageEvent;
        public event Action? OnConnectEvent;

        public WebSocketState State => webSocket.State;

        public WebSocketRpcTransport(Uri uri,
            int bufferSize = 100000)
        {
            this.uri = uri;
            receiveBuffer = new byte[bufferSize];
            webSocket = new DCLWebSocket();
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

            isDisposed = true;
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
                        int totalBytes = 0;
                        WebSocketReceiveResult result;

                        do
                        {
if (totalBytes >= receiveBuffer.Length)
    throw new InvalidOperationException($"Incoming RPC message exceeds receive buffer capacity ({receiveBuffer.Length} bytes)");

result = await webSocket.ReceiveAsync(
    new Memory<byte>(receiveBuffer, totalBytes, receiveBuffer.Length - totalBytes), ct);

                            if (result.MessageType == WebSocketMessageType.Close)
                                break;

                            totalBytes += result.Count;
                        }
                        while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            if (!string.IsNullOrEmpty(result.CloseStatusDescription))
                                ReportHub.LogError(ReportCategory.SOCIAL, $"Friends web socket disconnected. {result.CloseStatusDescription}");

                            await CloseAsync(ct);
                            break;
                        }

                        if (totalBytes > 0)
                        {
                            var data = new byte[totalBytes];
                            receiveBuffer.AsSpan(0, totalBytes).CopyTo(data);

                            try { OnMessageEvent?.Invoke(data); }
                            catch (Exception ex)
                            {
                                ReportHub.LogWarning(ReportCategory.SOCIAL,
                                    $"Failed to process incoming RPC message ({totalBytes} bytes): {ex.Message}");
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (WebSocketException e)
                    {
                        OnErrorEvent?.Invoke(e);
                        break;
                    }
                    catch (Exception e)
                    {
                        ReportHub.LogException(e, ReportCategory.SOCIAL);
                        webSocket.Abort();
                        OnErrorEvent?.Invoke(e);
                        break;
                    }
                }
            }
        }

        public virtual async UniTask SendMessageAsync(byte[] data, CancellationToken ct)
        {
            if (isDisposed || !isConnected) return;

            try { await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct); }
            catch (WebSocketException e)
            {
                OnErrorEvent?.Invoke(e);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public virtual async UniTask SendMessageAsync(string data, CancellationToken ct)
        {
            if (isDisposed || !isConnected) return;

            try { await webSocket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, ct); }
            catch (WebSocketException e)
            {
                OnErrorEvent?.Invoke(e);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        public void Close() =>
            CloseAsync(lifeCycleCancellationToken.Token).Forget();

        public async UniTask CloseAsync(CancellationToken ct)
        {
            if (isDisposed) return;

            if (State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                OnCloseEvent?.Invoke();
            }
        }
    }
}
