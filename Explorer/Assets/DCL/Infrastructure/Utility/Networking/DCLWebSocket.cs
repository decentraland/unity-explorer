using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Networking
{
    // Desktop / WebGL friendly implementation
    public class DCLWebSocket : IDisposable
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private DCL.WebSockets.JS.WebGLWebSocket ws = new ();
#else

        private System.Net.WebSockets.ClientWebSocket ws = new ();
#endif


        public WebSocketState State 
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
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
#if UNITY_WEBGL && !UNITY_EDITOR
                await ws.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
#else

                System.Net.WebSockets.WebSocketMessageType msgType = (System.Net.WebSockets.WebSocketMessageType) messageType;

                await ws.SendAsync(buffer, msgType, endOfMessage, cancellationToken);
#endif
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
#if UNITY_WEBGL && !UNITY_EDITOR
                return await ws.ReceiveAsync(buffer, cancellationToken);
#else
                System.Net.WebSockets.ValueWebSocketReceiveResult result = await ws.ReceiveAsync(buffer, cancellationToken);
                WebSocketMessageType msgType = (WebSocketMessageType) result.MessageType;
                WebSocketCloseStatus? closeStatus = ws.CloseStatus == null ? null : (WebSocketCloseStatus) ws.CloseStatus;
                return new WebSocketReceiveResult(
                        result.Count,
                        msgType,
                        result.EndOfMessage,
                        closeStatus,
                        ws.CloseStatusDescription
                        );
#endif
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

#if UNITY_WEBGL && !UNITY_EDITOR
                await ws.CloseAsync(status, description, cancellationToken);
#else
                System.Net.WebSockets.WebSocketCloseStatus statusType = (System.Net.WebSockets.WebSocketCloseStatus)status;
                await ws.CloseAsync(statusType, description, cancellationToken);
#endif
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public void Abort()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Ignore, WebGL doesn't expose raw TCP sockets to hard interrupt
#else
            ws.Abort();
#endif
        }
    }

}
