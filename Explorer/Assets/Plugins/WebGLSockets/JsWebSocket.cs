#if UNITY_WEBGL

using System;
using System.Threading;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Utility.Networking;

namespace DCL.WebSockets.JS 
{
    public sealed class WebGLWebSocket : IDisposable
    {
        [DllImport("__Internal")] private static extern int WS_New();
        [DllImport("__Internal")] private static extern void WS_Dispose(int id);

        [DllImport("__Internal")] private static extern int WS_State(int id);

        [DllImport("__Internal")] private static extern void WS_Connect(int id, string url);
        [DllImport("__Internal")] private static extern void WS_Close(int id);
        [DllImport("__Internal")] private static extern int WS_Send(int id, IntPtr dataPtr, int dataLen, int messageType);

        // -1 = nothing
        // 0 = binary
        // 1 = text
        [DllImport("__Internal")] private static extern int WS_NextAvailableToReceive(int id);

        // returns length, if buffer too small: -1
        [DllImport("__Internal")] private static extern int WS_TryConsumeNextReceived(int id, IntPtr bufferPtr, int bufferLen);

        private readonly int handleId;

        public WebSocketState State => (WebSocketState) WS_State(handleId);

        public WebGLWebSocket()
        {
            handleId = WS_New();
UnityEngine.Debug.Log($"JsWebSocket.cs: Ctor, handleId: {handleId}");
        }

        public void Dispose()
        {
            WS_Dispose(handleId);
UnityEngine.Debug.Log($"JsWebSocket.cs: Dispose, handleId: {handleId}");
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            UnityEngine.Debug.Log($"JsWebSocket.cs: SendAsync, handleId: {handleId}");

            cancellationToken.ThrowIfCancellationRequested();

            unsafe
            {
                fixed (byte* ptr = buffer.Span)
                {
                    int err = WS_Send(
                            handleId,
                            (IntPtr)ptr,
                            buffer.Length,
                            messageType == WebSocketMessageType.Binary ? 0 : 1
                            );
  // int error code
  // 0 = OK
  // 1 = invalid handle
  // 2 = invalid state
  // 3 = send failed

                    switch (err)
                    {
                        case 0:
                            return;

                        case 1:
                            throw new ObjectDisposedException("WebSocket");

                        case 2:
                            throw new InvalidOperationException("WebSocket is not open");

                        case 3:
                            throw new InvalidOperationException("WebSocket send failed");

                        default:
                            throw new InvalidOperationException($"Unknown WS_Send error: {err}");
                    }
                }
            }
        }

        public async UniTask<WebSocketReceiveResult> ReceiveAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken)
        {
            // Match System.Net.WebSockets behavior: Closed => Close frame
            if (State == WebSocketState.Closed)
            {
                return new WebSocketReceiveResult(
                        0,
                        WebSocketMessageType.Close,
                        endOfMessage: true,
                        closeStatus: null,
                        closeStatusDescription: null);
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int next = WS_NextAvailableToReceive(handleId);

                if (next == -1)
                {
                    // No data yet
                    // If socket transitioned to terminal state while waiting
                    if (State == WebSocketState.Closed || State == WebSocketState.Aborted)
                    {
                        return new WebSocketReceiveResult(
                                0,
                                WebSocketMessageType.Close,
                                endOfMessage: true,
                                closeStatus: null,
                                closeStatusDescription: null);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                    continue;
                }

                var messageType =
                    next == 0 ? WebSocketMessageType.Binary :
                    /* next == 1 */ WebSocketMessageType.Text;

                unsafe
                {
                    fixed (byte* ptr = buffer.Span)
                    {
                        int len = WS_TryConsumeNextReceived(handleId, (IntPtr)ptr, buffer.Length);

                        if (len == -1)
                            throw new InvalidOperationException("Receive buffer too small");

                        // One JS message == one WebSocket message (browser semantics)
                        return new WebSocketReceiveResult(
                                len,
                                messageType,
                                endOfMessage: true,
                                closeStatus: null,
                                closeStatusDescription: null);
                    }
                }
            }
        }

        public async UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log($"JsWebSocket.cs: ConnectAsync, handleId: {handleId}");
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            // Trigger JS-side connect (WebSocket ctor)
            WS_Connect(handleId, uri.AbsoluteUri);

            // Wait until OPEN / terminal state
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var state = (WebSocketState)WS_State(handleId);

                switch (state)
                {
                    case WebSocketState.Open:
                        return;

                    case WebSocketState.Closed:
                    case WebSocketState.Aborted:
                        throw new InvalidOperationException($"WebSocket connect failed. State={state}");
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public async UniTask CloseAsync(
                WebSocketCloseStatus status,
                string? description,
                CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log($"JsWebSocket.cs: CloseAsync, handleId: {handleId}");

            // status / description are advisory on WebGL (JS close() has no reliable mapping)
            WS_Close(handleId);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var state = (WebSocketState)WS_State(handleId);

                switch (state)
                {
                    case WebSocketState.Closed:
                        return;

                    case WebSocketState.Aborted:
                        throw new InvalidOperationException("WebSocket aborted during close");
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }
    }
}

#endif
