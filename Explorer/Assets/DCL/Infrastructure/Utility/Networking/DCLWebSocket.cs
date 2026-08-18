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

        // Failing a pending connection requires abandoning the connect await: once the TCP
        // connect succeeds, mono's ClientWebSocket keeps no registration that can unpark the
        // HTTP-upgrade read, so neither Abort() nor a CancellationToken completes a parked
        // ConnectAsync.
        private readonly CancellationTokenSource connectAbort = new ();
#endif

        public WebSocketState State
        {
            get
            {
#if UNITY_WEBGL && (!UNITY_EDITOR || EDITOR_DEBUG_WEBGL)
                return ws.State;
#else
                return (WebSocketState) ws.State; // Direct mapping
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
                // AttachExternalCancellation completes this await when connectAbort fires even
                // though the BCL task stays parked; the abandoned task's outcome must not
                // surface as an unobserved fault.
                //
                // The receive loop that consumes this connection is started in this continuation, so
                // the continuation must resume on the thread the connect was issued from. A connecting
                // thread that has a SynchronizationContext (the Unity main thread, whose social/comms
                // receive loops feed main-thread-only UI) keeps it so the loop stays on that thread; a
                // connecting thread without one (the V8 script-invoke thread) skips it, since
                // TaskScheduler.FromCurrentSynchronizationContext() throws with no current context
                // (see DCLSemaphoreSlim.WaitAsync).
                bool marshalBackToIssuingContext = SynchronizationContext.Current != null;
                using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, connectAbort.Token);
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
                // A close handshake requires an established connection: mono's ClientWebSocket marks
                // itself connected internally before the upgrade completes and its close path derefs an
                // inner socket that only exists after a successful upgrade, so closing a pending or
                // already torn-down connection must abort it instead (WHATWG close() during CONNECTING
                // fails the connection; close() on a closed socket is a no-op).
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
            // Ignore, WebGL doesn't expose raw TCP sockets to hard interrupt
#else
            // Cancelling connectAbort is what completes a parked connect await; ws.Abort() alone
            // leaves it hanging on mono. Cancelling after a completed connect is inert (the
            // linked registration is gone once ConnectAsync returns).
            try { connectAbort.Cancel(); }
            catch (ObjectDisposedException)
            {
                // Abort must tolerate a racing Dispose() (scene teardown vs a JS close())
            }

            ws.Abort();
#endif
        }
    }

}
