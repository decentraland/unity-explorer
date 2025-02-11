using Cysharp.Threading.Tasks;
using rpc_csharp.transport;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using UnityEngine;
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
                Debug.Log($"Friends.WebSocket.Disposing..");
                webSocket.Abort();
                webSocket.Dispose();
                Debug.Log($"Friends.WebSocket.Disposed");
            }
            catch (ObjectDisposedException) { }
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (State is WebSocketState.Open or WebSocketState.Connecting)
                throw new Exception("Web socket already connected");

            Debug.Log($"Friends.WebSocket.Connecting: {uri}");
            await webSocket.ConnectAsync(uri, ct);
            Debug.Log("Friends.WebSocket.Connected");

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

                        Debug.Log($"Friends.WebSocket.Received: Data size {result.Count}, data type: {result.MessageType}");

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
                Debug.Log($"Friends.WebSocket.Sending: data size {data.Length}");
                await webSocket.SendAsync(data, WebSocketMessageType.Binary, true, ct);
                Debug.Log($"Friends.WebSocket.Sent: data size {data.Length}");
            }
            catch (WebSocketException e)
            {
                Debug.Log($"Friends.WebSocket.Send.Error: {e.Message}");
                OnErrorEvent?.Invoke(e.Message);
            }
        }

        public async UniTask SendMessageAsync(string data, CancellationToken ct)
        {
            try
            {
                Debug.Log($"Friends.WebSocket.Sending: data size {data.Length}");
                await webSocket.SendAsync(Encoding.UTF8.GetBytes(data), WebSocketMessageType.Text, true, ct);
                Debug.Log($"Friends.WebSocket.Sent: data size {data.Length}");
            }
            catch (WebSocketException e)
            {
                Debug.Log($"Friends.WebSocket.Send.Error: {e.Message}");
                OnErrorEvent?.Invoke(e.Message);
            }
        }

        public void Close() =>
            CloseAsync(lifeCycleCancellationToken.Token).Forget();

        public async UniTask CloseAsync(CancellationToken ct)
        {
            if (State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                Debug.Log("Friends.WebSocket.Disconnecting..");
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
                Debug.Log("Friends.WebSocket.Disconnected");
                OnCloseEvent?.Invoke();
            }
        }
    }
}
