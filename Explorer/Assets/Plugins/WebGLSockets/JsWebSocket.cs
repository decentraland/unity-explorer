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

        /* TODO
        [DllImport("__Internal")] private static extern void WS_BeginConnect(int id, string url);
        [DllImport("__Internal")] private static extern void WS_SendText(int id, string msg, int len);
        [DllImport("__Internal")] private static extern void WS_SendBinary(int id, IntPtr ptr, int len);
        [DllImport("__Internal")] private static extern int  WS_Poll(int id);
        [DllImport("__Internal")] private static extern int  WS_Dequeue(int id, IntPtr outPtr);
        [DllImport("__Internal")] private static extern void WS_Free(IntPtr ptr);
        */


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
UnityEngine.Debug.Log("JsWebSocket.cs:50"); // SPECIAL_DEBUG_LINE_STATEMENT
        }

        public async UniTask<WebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log("JsWebSocket.cs:55"); // SPECIAL_DEBUG_LINE_STATEMENT
            while (cancellationToken.IsCancellationRequested == false)
                await UniTask.Yield();

UnityEngine.Debug.Log("JsWebSocket.cs:59"); // SPECIAL_DEBUG_LINE_STATEMENT
            throw new Exception();
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
