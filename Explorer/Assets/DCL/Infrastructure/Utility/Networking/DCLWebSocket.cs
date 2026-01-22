using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Networking
{
    // Desktop / WebGL friendly implementation
    // TODO consider throwing System.Net.WebSockets.WebSocketException
    public class DCLWebSocket : IDisposable
    {
#if UNITY_WEBGL
        // TODO
#else

        private System.Net.WebSockets.ClientWebSocket ws = new ();
#endif


        public WebSocketState State 
        {
            get
            {

#if UNITY_WEBGL
            // TODO
               throw new Exception(); //return (WSState) ws.State; // Direct mapping
#else

                return (WebSocketState) ws.State; // Direct mapping
#endif
            }
        }

        public void Dispose()
        {
#if UNITY_WEBGL
            // TODO
#else

               ws.Dispose();
#endif
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

        public void Abort()
        {
#if UNITY_WEBGL
            // Ignore
#else
            ws.Abort();
#endif
        }
    }

}
