using System;
using System.Net;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Networking
{
    public class DCLWebSocketOptions
    {
#if !WEBGL_ACTIVE
        private readonly System.Net.WebSockets.ClientWebSocketOptions options;

        internal DCLWebSocketOptions(System.Net.WebSockets.ClientWebSocketOptions options)
        {
            this.options = options;
        }

        public IWebProxy Proxy
        {
            get => options.Proxy;
            set => options.Proxy = value;
        }

        public void SetRequestHeader(string headerName, string headerValue) =>
            options.SetRequestHeader(headerName, headerValue);
#else
        // WebGL WebSocket API does not support custom headers or proxy settings
        public IWebProxy? Proxy { get; set; }

        public void SetRequestHeader(string headerName, string headerValue) { }
#endif
    }

    // Desktop / WebGL friendly implementation
    public class DCLWebSocket : IDisposable
    {
#if WEBGL_ACTIVE
        private DCL.WebSockets.JS.WebGLWebSocket ws = new ();
        private readonly DCLWebSocketOptions options = new ();
#else
        private System.Net.WebSockets.ClientWebSocket ws = new ();
        private DCLWebSocketOptions? options;
#endif

        public DCLWebSocketOptions Options
        {
            get
            {
#if WEBGL_ACTIVE
                return options;
#else
                return options ??= new DCLWebSocketOptions(ws.Options);
#endif
            }
        }


        public WebSocketState State
        {
            get
            {
#if WEBGL_ACTIVE
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
#if WEBGL_ACTIVE
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
#if WEBGL_ACTIVE
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

#if WEBGL_ACTIVE
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
#if WEBGL_ACTIVE
            // Ignore, WebGL doesn't expose raw TCP sockets to hard interrupt
#else
            ws.Abort();
#endif
        }
    }

}
