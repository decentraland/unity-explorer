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
        /*
        [DllImport("__Internal")] private static extern int  WS_Create();
        [DllImport("__Internal")] private static extern void WS_Connect(int id, string url);
        [DllImport("__Internal")] private static extern void WS_SendText(int id, string msg, int len);
        [DllImport("__Internal")] private static extern void WS_SendBinary(int id, IntPtr ptr, int len);
        [DllImport("__Internal")] private static extern int  WS_Poll(int id);
        [DllImport("__Internal")] private static extern int  WS_Dequeue(int id, IntPtr outPtr);
        [DllImport("__Internal")] private static extern void WS_Close(int id);
        [DllImport("__Internal")] private static extern void WS_Destroy(int id);
        [DllImport("__Internal")] private static extern void WS_Free(IntPtr ptr);
        */

        private readonly int handleId;

        public WebSocketState State 
        {
            get
            {
            // TODO
            throw new NotImplementedException();
            }
        }

        public void Dispose()
        {
            //TODO
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            //TODO
        }

        public async UniTask<WebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            //TODO
            throw new NotImplementedException();
        }

        public async UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            //TODO
        }

        public async UniTask CloseAsync(WebSocketCloseStatus status, String? description, CancellationToken CancellationToken)
        {
            //TODO
        }
    }
}

#endif
