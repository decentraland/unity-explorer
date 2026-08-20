using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Utility.Networking
{
    /// <summary>
    ///     Desktop / WebGL friendly implementation. CloseAsync, Abort and Dispose may run on a
    ///     different thread than a pending ConnectAsync await; failing a pending connection
    ///     completes that await instead of leaving it parked.
    /// </summary>
    public class DCLWebSocket : IDisposable
    {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
        private DCL.WebSockets.JS.WebGLWebSocket ws = new ();
#else
        private System.Net.WebSockets.ClientWebSocket ws = new ();

        // Once TCP connects, neither Abort() nor a CancellationToken unparks mono's ConnectAsync, so failing a pending connection means abandoning its await via this token.
        private readonly CancellationTokenSource connectAbort = new ();
#endif

        public WebSocketState State
        {
            get
            {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
                return ws.State;
#else
                return (WebSocketState) ws.State;
#endif
            }
        }

        public void Dispose()
        {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
            ws.Dispose();
#else
            connectAbort.SafeCancelAndDispose();
            ws.Dispose();
#endif
        }

        public async UniTask SendAsync(ReadOnlyMemory<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            try
            {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
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
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
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
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
                await ws.ConnectAsync(uri, cancellationToken);
#else
                // Main-thread callers need the continuation back on their context (their receive loops feed main-thread-only UI); context-less ones (the V8 script-invoke thread) would throw in TaskScheduler.FromCurrentSynchronizationContext.
                bool marshalBackToIssuingContext = SynchronizationContext.Current != null;
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectAbort.Token);

                // Only AttachExternalCancellation can complete this await once the BCL task parks mid-upgrade (see connectAbort).
                await ws.ConnectAsync(uri, linked.Token).AsUniTask(useCurrentSynchronizationContext: marshalBackToIssuingContext).AttachExternalCancellation(linked.Token);
#endif
            }
            catch (System.Net.WebSockets.WebSocketException e)
            {
                throw new WebSocketException(e);
            }
        }

        public async UniTask CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken cancellationToken)
        {
            try
            {

#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
                await ws.CloseAsync(status, description, cancellationToken);
#else
                // Mono's close path derefs an inner socket that only exists after a successful upgrade; per WHATWG, close() outside an established connection aborts instead.
                if (State is not (WebSocketState.Open or WebSocketState.CloseReceived or WebSocketState.CloseSent))
                {
                    Abort();
                    return;
                }

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
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
            // WebGL doesn't expose raw TCP sockets to hard-interrupt.
#else
            // ws.Abort() alone leaves a parked connect hanging on mono; cancelling after a completed connect is inert.
            try { connectAbort.Cancel(); }
            catch (ObjectDisposedException)
            {
                // A racing Dispose() (scene teardown vs a JS close()) may have already disposed the CTS.
            }

            ws.Abort();
#endif
        }
    }

}
