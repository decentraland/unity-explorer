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

        private DisconnectHandler? disconnectHandler;
        private HandshakeHandler? handshakeHandler;
        private CancellationTokenSource? connectionLifeCycleCts;
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
            UnregisterAllHandlers();
            transport.Dispose();
        }

        public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, IncomingMessageHandler handler)
        {
            syncHandlers[type] = handler;
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
            disconnectHandler = null;
            handshakeHandler = null;
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (transport.State is ITransport.TransportState.CONNECTED or ITransport.TransportState.CONNECTING)
                return;

            await ConnectWithRetriesAsync(ct);
        }

        public UniTask DisconnectAsync()
        {
            connectionLifeCycleCts.SafeCancelAndDispose();
            disconnectHandler?.Invoke(DisconnectReason.GRACEFUL);
            return transport.DisconnectAsync(DisconnectReason.GRACEFUL);
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
            if (transport.State != ITransport.TransportState.CONNECTED)
            {
                outgoingMessage.Dispose();
                return;
            }

            pipe.Send(outgoingMessage);
        }

        private async UniTask ConnectWithRetriesAsync(CancellationToken ct)
        {
            var attempt = 1;

            while (true)
            {
                try
                {
                    await ConnectInternalAsync(ct);
                    return;
                }
                catch (TimeoutException)
                {
                    (bool canBeRepeated, TimeSpan retryDelay) = WebRequestUtils.CanBeRepeated(attempt, CONNECTION_RETRY_POLICY, true, null);

                    if (canBeRepeated)
                    {
                        ReportHub.Log(ReportCategory.MULTIPLAYER, $"Pulse connection attempt {attempt} timed out, retrying in {retryDelay}");

                        // Task instead of UniTask is used to respect the original thread / synchronization context to avoid continuation on the main thread

                        await DCLTask.Delay(retryDelay, ct);
                    }
                    else
                    {
                        ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection won't be restored after attempt {attempt}");
                        return;
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
            StartRouting(connectionLifeCycleCts.Token, ct);

            // Handshake exchange runs through the registered handler (PulseMultiplayerBus owns
            // request assembly, auth chain construction, response correlation). The handler is
            // expected to throw on a failed handshake — propagate the exception.
            if (handshakeHandler != null)
                await handshakeHandler(handshakeCompletion, ct);

            isAuthenticated = true;
        }

        private void StartRouting(CancellationToken connectionCt, CancellationToken parentCt)
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
                                    (bool reconnectionAllowed, TimeSpan reconnectionDelay) = disconnectHandler?.Invoke(disconnectEvent) ?? (false, TimeSpan.Zero);

                                    if (reconnectionAllowed && !parentCt.IsCancellationRequested)
                                    {
                                        ResetConnectionLifecycle();

                                        ReportHub.Log(ReportCategory.MULTIPLAYER, "Attempting reconnection...");

                                        await DCLTask.Delay(reconnectionDelay, parentCt);

                                        try { await ConnectAsync(parentCt); }
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
                                        handler(message);
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
