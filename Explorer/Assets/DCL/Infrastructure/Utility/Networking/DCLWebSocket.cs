using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Networking
{
    // Desktop / WebGL friendly implementation
    public class DCLWebSocket : IDisposable
    {
#if UNITY_WEBGL
        private DCL.WebSockets.JS.WebGLWebSocket ws = new ();
#else

        private System.Net.WebSockets.ClientWebSocket ws = new ();
#endif


        public WebSocketState State 
        {
            get
            {
#if UNITY_WEBGL
                return ws.State;
#else
                return (WebSocketState) ws.State; // Direct mapping
#endif
            }
        }

        public void Dispose()
        {
            ws.Dispose();
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            try
            {
                await ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public async UniTask<WebSocketReceiveResult> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            try
            {
                return await ws.ReceiveAsync(buffer, cancellationToken);
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public async UniTask ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                await ws.ConnectAsync(uri, cancellationToken);
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public async UniTask CloseAsync(WebSocketCloseStatus status, String? description, CancellationToken cancellationToken)
        {
            try
            {
                await ws.CloseAsync(status, description, cancellationToken);
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public void Abort()
        {
#if UNITY_WEBGL
            // Ignore, WebGL doesn't expose raw TCP sockets to hard interrupt
#else
            ws.Abort();
#endif
        }
    }

}
