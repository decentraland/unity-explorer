using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using Decentraland.Pulse;
using Pulse.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Pulse
{
    using static IPulseMultiplayerService;

    public class PulseMultiplayerService : IPulseMultiplayerService
    {
        private const int PORT = 7777;
        private static readonly RetryPolicy CONNECTION_RETRY_POLICY = RetryPolicy.WithRetries(int.MaxValue, 1000, 2);

        private readonly ITransport transport;
        private readonly MessagePipe pipe;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly Dictionary<ServerMessage.MessageOneofCase, IncomingMessageHandler> syncHandlers = new ();

        private Action? beforeMessageHandler;
        private DisconnectHandler? disconnectHandler;
        private HandshakeHandler? handshakeHandler;
        private CancellationTokenSource? connectionLifeCycleCts;

        /// <summary>
        ///     Spans one explicit <see cref="ConnectAsync" /> up to the matching <see cref="DisconnectAsync" />,
        ///     including every automatic reconnection in between. Cancelling it stops the background
        ///     reconnection loop as well as the attempt in flight.
        /// </summary>
        private CancellationTokenSource? sessionCts;

        /// <summary>
        ///     The connection attempt still in flight, shared with concurrent callers so a second call cannot
        ///     start a second transport connection or handshake. A completion source rather than the attempt's
        ///     own task because several callers await it at once.
        /// </summary>
        private UniTaskCompletionSource<bool>? connectAttempt;
        private volatile bool isAuthenticated;

        public PulseMultiplayerService(
            ITransport transport,
            MessagePipe pipe,
            IDecentralandUrlsSource urlsSource)
        {
            this.transport = transport;
            this.pipe = pipe;
            this.urlsSource = urlsSource;
        }

        public bool IsAuthenticated => isAuthenticated;

        public void Dispose()
        {
            isAuthenticated = false;
            sessionCts.SafeCancelAndDispose();
            sessionCts = null;
            connectAttempt = null;
            connectionLifeCycleCts.SafeCancelAndDispose();
            UnregisterAllHandlers();
            transport.Dispose();
        }

        public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, IncomingMessageHandler handler)
        {
            syncHandlers[type] = handler;
        }

        public void RegisterBeforeMessageHandler(Action handler)
        {
            beforeMessageHandler = handler;
        }

        public void RegisterDisconnectHandler(DisconnectHandler handler)
        {
            disconnectHandler = handler;
        }

        public void RegisterHandshakeHandler(HandshakeHandler handler)
        {
            handshakeHandler = handler;
        }

        public void UnregisterAllHandlers()
        {
            syncHandlers.Clear();
            beforeMessageHandler = null;
            disconnectHandler = null;
            handshakeHandler = null;
        }

        public UniTask<bool> ConnectAsync(CancellationToken ct, int maxAttempts = int.MaxValue)
        {
            if (connectAttempt != null)
                return connectAttempt.Task;

            if (transport.State is ITransport.TransportState.Connected or ITransport.TransportState.Connecting)
                return UniTask.FromResult(true);

            sessionCts = sessionCts.SafeRestartLinked(ct);
            return StartAttempt(sessionCts.Token, maxAttempts);
        }

        public async UniTask DisconnectAsync()
        {
            if (sessionCts == null)
                return;

            // Cleared before the transport teardown so a ConnectAsync issued meanwhile starts a fresh session
            // instead of joining the attempt being cancelled
            sessionCts.SafeCancelAndDispose();
            sessionCts = null;
            connectAttempt = null;
            connectionLifeCycleCts.SafeCancelAndDispose();

            // Waits for the ENet listen loop to exit, so no further event reaches the routing loop
            // (already cancelled above) before the disconnect handler clears the peer collections
            await transport.DisconnectAsync(DisconnectReason.GRACEFUL);

            isAuthenticated = false;
            disconnectHandler?.Invoke(DisconnectReason.GRACEFUL);
        }

        /// <summary>
        ///     Cancels the current connection lifecycle (message routing, subscriptions).
        ///     Must be called before reconnecting after a transport-level disconnect.
        /// </summary>
        private void ResetConnectionLifecycle()
        {
            isAuthenticated = false;
            connectionLifeCycleCts.SafeCancelAndDispose();
        }

        public void Send(OutgoingMessage outgoingMessage)
        {
            if (transport.State != ITransport.TransportState.Connected)
            {
                outgoingMessage.Dispose();
                return;
            }

            pipe.Send(outgoingMessage);
        }

        /// <summary>
        ///     Single flight per session: an attempt already in flight — explicit or automatic reconnection —
        ///     is handed to every caller instead of starting a second transport connection and handshake.
        /// </summary>
        private UniTask<bool> StartAttempt(CancellationToken sessionCt, int maxAttempts)
        {
            if (connectAttempt != null)
                return connectAttempt.Task;

            // The session may have been ended between the caller's check and this point
            if (sessionCt.IsCancellationRequested)
                return UniTask.FromCanceled<bool>(sessionCt);

            var attempt = new UniTaskCompletionSource<bool>();
            connectAttempt = attempt;
            RunAttemptAsync(attempt, sessionCt, maxAttempts).Forget();
            return attempt.Task;
        }

        private async UniTaskVoid RunAttemptAsync(UniTaskCompletionSource<bool> attempt, CancellationToken sessionCt, int maxAttempts)
        {
            bool connected;

            try { connected = await ConnectWithRetriesAsync(sessionCt, maxAttempts); }
            catch (Exception e)
            {
                ReleaseAttempt(attempt);

                if (e is OperationCanceledException)
                    attempt.TrySetCanceled(sessionCt);
                else
                    attempt.TrySetException(e);

                return;
            }

            ReleaseAttempt(attempt);
            attempt.TrySetResult(connected);
        }

        /// <summary>
        ///     Cleared before the attempt is settled, so an awaiter that reacts by starting a new attempt
        ///     gets a fresh one. A DisconnectAsync that ran meanwhile already cleared it and may have been
        ///     followed by a newer session whose attempt must stay registered.
        /// </summary>
        private void ReleaseAttempt(UniTaskCompletionSource<bool> attempt)
        {
            if (ReferenceEquals(connectAttempt, attempt))
                connectAttempt = null;
        }

        /// <summary>
        ///     An explicit attempt that was in flight when the transport dropped is awaited first: joining it
        ///     would report its outcome for a connection that is already gone.
        /// </summary>
        private async UniTask ReconnectAsync(CancellationToken sessionCt)
        {
            UniTaskCompletionSource<bool>? inFlight = connectAttempt;

            if (inFlight != null)
            {
                // Its outcome belongs to the callers that started it; only its completion matters here
                try { await inFlight.Task; }
                catch (Exception) { }
            }

            await StartAttempt(sessionCt, int.MaxValue);
        }

        private async UniTask<bool> ConnectWithRetriesAsync(CancellationToken ct, int maxAttempts)
        {
            var attempt = 1;

            while (true)
            {
                try
                {
                    await ConnectInternalAsync(ct);
                    return true;
                }
                catch (PulseHandshakeDisconnectedException e) when (!e.IsRetriable)
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection failed terminally, no retry: {e.Message}");
                    return false;
                }
                catch (Exception e) when (e is TimeoutException or PulseHostResolutionException or PulseHandshakeDisconnectedException)
                {
                    if (attempt >= maxAttempts)
                    {
                        ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection won't be restored after attempt {attempt}");
                        return false;
                    }

                    (bool canBeRepeated, TimeSpan retryDelay) = WebRequestUtils.CanBeRepeated(attempt, CONNECTION_RETRY_POLICY, true, null);

                    if (canBeRepeated)
                    {
                        ReportHub.Log(ReportCategory.MULTIPLAYER, $"Pulse connection attempt {attempt} failed ({e.Message}), retrying in {retryDelay}");

                        // Task instead of UniTask is used to respect the original thread / synchronization context to avoid continuation on the main thread

                        await DCLTask.Delay(retryDelay, ct);
                    }
                    else
                    {
                        ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection won't be restored after attempt {attempt}");
                        return false;
                    }
                }

                attempt++;
            }
        }

        private async UniTask ConnectInternalAsync(CancellationToken ct)
        {
            await transport.ConnectAsync(urlsSource.Url(DecentralandUrl.Pulse), PORT, ct);

            // Register handshake handler before starting the routing loop so it's visible immediately.
            // Extract fields inside the handler — the underlying proto message is returned to pool after the handler returns.
            var handshakeCompletion = new UniTaskCompletionSource<(bool success, string? error)>();

            // Registered one shot here, not in the handler to prevent a circular dependency
            syncHandlers[ServerMessage.MessageOneofCase.Handshake] = message =>
            {
                syncHandlers.Remove(ServerMessage.MessageOneofCase.Handshake);
                HandshakeResponse response = message.Message.Handshake;
                handshakeCompletion.TrySetResult((response.Success, response.HasError ? response.Error : null));
            };

            connectionLifeCycleCts = connectionLifeCycleCts.SafeRestartLinked(ct);
            StartRouting(handshakeCompletion, connectionLifeCycleCts.Token, ct);

            // Handshake exchange runs through the registered handler (PulseMultiplayerBus owns
            // request assembly, auth chain construction, response correlation). The handler is
            // expected to throw on a failed handshake — propagate the exception.
            if (handshakeHandler != null)
                await handshakeHandler(handshakeCompletion, ct);
            else
                // No handshake to await — settle the completion so a later disconnect takes the
                // reconnection path instead of the handshake-failure path.
                handshakeCompletion.TrySetResult((true, null));

            isAuthenticated = true;
        }

        private void StartRouting(UniTaskCompletionSource<(bool success, string? error)> handshakeCompletion, CancellationToken connectionCt, CancellationToken sessionCt)
        {
            // RunOnThreadPool with configureAwait: false ensures all await continuations
            // stay on the thread pool — matching the ENet transport pattern.
            // UniTask.Delay is NOT used here because it schedules on the Unity player loop
            // and would resume on the main thread; Task.Delay respects the null
            // SynchronizationContext of thread pool threads.
            DCLTask.RunOnThreadPool(async () =>
                    {
                        try
                        {
                            await foreach (MessagePipeEvent evt in pipe.ReadEventsAsync(connectionCt))
                            {
                                if (evt.IsDisconnectEvent(out MessagePipeEvent.DisconnectEvent disconnectEvent))
                                {
                                    // The server may drop the connection before sending a HandshakeResponse.
                                    // Fault the pending handshake instead of reconnecting from here — recovery is
                                    // owned by the connection attempt awaiting it, and a competing reconnection
                                    // would strand that attempt on a response that will never arrive.
                                    if (handshakeCompletion.TrySetException(new PulseHandshakeDisconnectedException(disconnectEvent.Reason)))
                                        break;

                                    (bool reconnectionAllowed, TimeSpan reconnectionDelay) = disconnectHandler?.Invoke(disconnectEvent) ?? (false, TimeSpan.Zero);

                                    // Both the delay and the attempt run under the session token so an explicit
                                    // DisconnectAsync ends the reconnection instead of racing a new connection
                                    if (reconnectionAllowed && !sessionCt.IsCancellationRequested)
                                    {
                                        ResetConnectionLifecycle();

                                        ReportHub.Log(ReportCategory.MULTIPLAYER, "Attempting reconnection...");

                                        await DCLTask.Delay(reconnectionDelay, sessionCt);

                                        try { await ReconnectAsync(sessionCt); }
                                        catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.MULTIPLAYER); }
                                        finally
                                        {
                                            if (PlayerLoopHelper.IsMainThread)
                                                await DCLTask.SwitchToThreadPool();
                                        }
                                    }

                                    break;
                                }

                                if (!evt.IsMessage(out IncomingMessage message)) continue;

                                try
                                {
                                    if (syncHandlers.TryGetValue(message.Message.MessageCase, out IncomingMessageHandler? handler))
                                    {
                                        beforeMessageHandler?.Invoke();
                                        handler(message);
                                    }
                                }
                                catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.MULTIPLAYER); }
                                finally { evt.Dispose(); }
                            }
                        }
                        catch (OperationCanceledException) { }
                    }, configureAwait: false, cancellationToken: connectionCt)
                   .Forget();
        }
    }
}
