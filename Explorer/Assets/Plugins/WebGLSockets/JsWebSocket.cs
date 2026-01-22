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
        /* TODO
        [DllImport("__Internal")] private static extern int  WS_New();
        [DllImport("__Internal")] private static extern void WS_BeginConnect(int id, string url);
        [DllImport("__Internal")] private static extern void WS_SendText(int id, string msg, int len);
        [DllImport("__Internal")] private static extern void WS_SendBinary(int id, IntPtr ptr, int len);
        [DllImport("__Internal")] private static extern int  WS_Poll(int id);
        [DllImport("__Internal")] private static extern int  WS_Dequeue(int id, IntPtr outPtr);
        [DllImport("__Internal")] private static extern void WS_Close(int id);
        [DllImport("__Internal")] private static extern void WS_Destroy(int id);
        [DllImport("__Internal")] private static extern void WS_Free(IntPtr ptr);
        */


        private readonly int handleId;
        private WebSocketState state;

        public WebSocketState State => state;

        public WebGLWebSocket()
        {
UnityEngine.Debug.Log("JsWebSocket.cs:33"); // SPECIAL_DEBUG_LINE_STATEMENT
            handleId = 0;//WS_New();
        }

        public void Dispose()
        {
UnityEngine.Debug.Log("JsWebSocket.cs:39"); // SPECIAL_DEBUG_LINE_STATEMENT
            /*
            WS_Destroy(handleId);
            */
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log("JsWebSocket.cs:47"); // SPECIAL_DEBUG_LINE_STATEMENT
            /*
            if (state != WebSocketState.Open)
                throw new InvalidOperationException();

            if (messageType == WebSocketMessageType.Text)
            {
                var text = System.Text.Encoding.UTF8.GetString(buffer.Span);
                WS_SendText(handleId, text, text.Length);
            }
            else if (messageType == WebSocketMessageType.Binary)
            {
                unsafe
                {
                    fixed (byte* ptr = buffer.Span)
                        WS_SendBinary(handleId, (IntPtr)ptr, buffer.Length);
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            await UniTask.CompletedTask;
            */
        }

        public async UniTask<WebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log("JsWebSocket.cs:76"); // SPECIAL_DEBUG_LINE_STATEMENT
            while (cancellationToken.IsCancellationRequested == false)
                await UniTask.Yield();

UnityEngine.Debug.Log("JsWebSocket.cs:80"); // SPECIAL_DEBUG_LINE_STATEMENT
            throw new Exception();
            /*
            while (WS_Poll(handleId) == 0)
                await UniTask.Yield();

            int type;
            int ptr;
            int len;

            unsafe
            {
                int* tmp = stackalloc int[2];
                type = WS_Dequeue(handleId, (IntPtr)tmp);
                ptr  = tmp[0];
                len  = tmp[1];
            }

            switch (type)
            {
                case 0: // open
                    state = WebSocketState.Open;
                    return null;

                case 1: // close
                    state = WebSocketState.Closed;
                    return new WebSocketReceiveResult(
                            0,
                            WebSocketMessageType.Close,
                            true);

                case 2: // error
                    state = WebSocketState.Aborted;
                    throw new WebSocketException("WebSocket error");

                case 3: // text
                case 4: // binary
                    int copyLen = Math.Min(len, buffer.Length);
                    var array = buffer.Array!;
                    Marshal.Copy((IntPtr)ptr, array, buffer.Offset, copyLen);
                    WS_Free((IntPtr)ptr);

                    return new WebSocketReceiveResult(
                            copyLen,
                            type == 3
                            ? WebSocketMessageType.Text
                            : WebSocketMessageType.Binary,
                            true);
            }

            throw new InvalidOperationException();
            */
        }

        public async UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
UnityEngine.Debug.Log("JsWebSocket.cs:136"); // SPECIAL_DEBUG_LINE_STATEMENT
            /*
            if (state != WebSocketState.None)
                throw new InvalidOperationException();

            state = WebSocketState.Connecting;
            WS_BeginConnect(handleId, uri.ToString());

            // browser connect is async but event-driven
            // state becomes Open once first open event is observed
            while (state == WebSocketState.Connecting)
                await UniTask.Yield();
            */
        }

        public async UniTask CloseAsync(WebSocketCloseStatus status, String? description, CancellationToken CancellationToken)
        {
UnityEngine.Debug.Log("JsWebSocket.cs:153"); // SPECIAL_DEBUG_LINE_STATEMENT
            /*
            WS_Destroy(handleId);
            state = WebSocketState.Closed;
            */
        }
    }
}

#endif
