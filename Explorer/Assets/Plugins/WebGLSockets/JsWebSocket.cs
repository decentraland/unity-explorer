#if UNITY_WEBGL

using System;
using System.Threading;
using System.Runtime.InteropServices;
using Cysharp.Threading.Tasks;
using Utility.Networking;
using RichTypes;

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

        public WebSocketState State
        {
            get
            {
                int state = WS_State(handleId);
                if (state == -1) throw new Exception($"State does not exist on js side for handle: {handleId}");
                return (WebSocketState) state;
            }
        }

        public WebGLWebSocket()
        {
            handleId = WS_New();
        }

        public void Dispose()
        {
            WS_Dispose(handleId);
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
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
            Result result = await ConnectAsyncInternal(uri, cancellationToken);
            if (result.Success)
            {
                return;
            }
            // Connect failed (State=Closed/Aborted). Caller will typically dispose; use LogWarning so stack trace isn't mistaken for a crash.
            UnityEngine.Debug.LogWarning($"JsWebSocket.cs: Connect failed, handleId: {handleId}, message: {result.ErrorMessage}, uri: {uri}. Caller may dispose.");
            throw new WebSocketException();
        }

        private async UniTask<Result> ConnectAsyncInternal(Uri uri, CancellationToken cancellationToken)
        {
            if (uri == null)
            {
                return Result.ErrorResult($"Invalid argument: uri");
            }

            // Trigger JS-side connect (WebSocket ctor)
            WS_Connect(handleId, uri.AbsoluteUri);

            // Wait until OPEN / terminal state
            int pollCount = 0;
            while (true)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Result.ErrorResult($"WebSocket connect cancelled. State={State}");
                }

                var state = (WebSocketState)WS_State(handleId);
                pollCount++;

                switch (state)
                {
                    case WebSocketState.Open:
                        return Result.SuccessResult();

                    case WebSocketState.Closed:
                    case WebSocketState.Aborted:
                        return Result.ErrorResult($"WebSocket connect failed. State={state}");
                }

                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
            }
        }

        public async UniTask CloseAsync(
                WebSocketCloseStatus status,
                string? description,
                CancellationToken cancellationToken)
        {
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
