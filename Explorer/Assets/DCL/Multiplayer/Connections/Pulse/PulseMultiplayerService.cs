using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Decentraland.Pulse;
using Google.Protobuf;
using Newtonsoft.Json;
using Pulse.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Utility;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseMultiplayerService : IDisposable
    {
        private const int MAX_CONNECT_ATTEMPTS = 3;
        private const int RECONNECTION_DELAY_MS = 10000;

        private readonly ITransport transport;
        private readonly MessagePipe pipe;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<ServerMessage.MessageOneofCase, Action<IncomingMessage>> syncHandlers = new ();
        private readonly Dictionary<string, string> authChainBuffer = new ();

        private Func<DisconnectReason, bool>? disconnectHandler;
        private CancellationTokenSource? connectionLifeCycleCts;
        private volatile bool isAuthenticated;

        public PulseMultiplayerService(
            ITransport transport,
            MessagePipe pipe,
            IWeb3IdentityCache identityCache)
        {
            this.transport = transport;
            this.pipe = pipe;
            this.identityCache = identityCache;
        }

        public bool IsAuthenticated => isAuthenticated;

        public void Dispose()
        {
            isAuthenticated = false;
            UnregisterAllHandlers();
            connectionLifeCycleCts.SafeCancelAndDispose();
            transport.Dispose();
        }

        public void RegisterSyncHandler(ServerMessage.MessageOneofCase type, Action<IncomingMessage> handler)
        {
            syncHandlers.Add(type, handler);
        }

        public void RegisterDisconnectHandler(Func<DisconnectReason, bool> handler)
        {
            disconnectHandler = handler;
        }

        public void UnregisterAllHandlers()
        {
            syncHandlers.Clear();
            disconnectHandler = null;
        }

        public async UniTask ConnectAsync(CancellationToken ct)
        {
            if (transport.State is ITransport.TransportState.CONNECTED or ITransport.TransportState.CONNECTING)
                return;

            await ConnectWithRetriesAsync(ct);
        }

        public async UniTask DisconnectAsync(CancellationToken ct)
        {
            connectionLifeCycleCts.SafeCancelAndDispose();
            await transport.DisconnectAsync(DisconnectReason.GRACEFUL, ct);
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
            for (var attempt = 1; attempt <= MAX_CONNECT_ATTEMPTS; attempt++)
            {
                try
                {
                    await ConnectInternalAsync(ct);
                    return;
                }
                catch (TimeoutException) when (attempt < MAX_CONNECT_ATTEMPTS)
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Pulse connection attempt {attempt}/{MAX_CONNECT_ATTEMPTS} timed out, retrying...");
                }
            }
        }

        private async UniTask ConnectInternalAsync(CancellationToken ct)
        {
            // TODO: get the address from IDecentralandUrlsSource (?)
            await transport.ConnectAsync("127.0.0.1", 7777, ct);

            // Register handshake handler before starting the routing loop so it's visible immediately.
            // Extract fields inside the handler — the underlying proto message is returned to pool after the handler returns.
            var handshakeCompletion = new UniTaskCompletionSource<(bool success, string? error)>();

            syncHandlers[ServerMessage.MessageOneofCase.Handshake] = message =>
            {
                syncHandlers.Remove(ServerMessage.MessageOneofCase.Handshake);
                HandshakeResponse response = message.Message.Handshake;
                handshakeCompletion.TrySetResult((response.Success, response.HasError ? response.Error : null));
            };

            connectionLifeCycleCts = connectionLifeCycleCts.SafeRestartLinked(ct);
            StartRouting(connectionLifeCycleCts.Token, ct);

            var handshakePacket = OutgoingMessage.Create(PacketMode.RELIABLE, ClientMessage.MessageOneofCase.Handshake);
            handshakePacket.Message.Handshake.AuthChain = ByteString.CopyFromUtf8(BuildAuthChain());

            Send(handshakePacket);

            (bool success, string? error) = await handshakeCompletion.Task;

            if (!success)
            {
                await DisconnectAsync(ct);
                throw new PulseException(error ?? "Handshake failed");
            }

            isAuthenticated = true;
        }

        private void StartRouting(CancellationToken connectionCt, CancellationToken parentCt)
        {
            // RunOnThreadPool with configureAwait: false ensures all await continuations
            // stay on the thread pool — matching the ENet transport pattern.
            // UniTask.Delay is NOT used here because it schedules on the Unity player loop
            // and would resume on the main thread; Task.Delay respects the null
            // SynchronizationContext of thread pool threads.
            UniTask.RunOnThreadPool(async () =>
            {
                try
                {
                    await foreach (MessagePipeEvent evt in pipe.ReadEventsAsync(connectionCt))
                    {
                        if (evt.IsDisconnectEvent(out MessagePipeEvent.DisconnectEvent disconnectEvent))
                        {
                            bool shouldReconnect = disconnectHandler?.Invoke(disconnectEvent) ?? false;

                            if (shouldReconnect && !parentCt.IsCancellationRequested)
                            {
                                ResetConnectionLifecycle();

                                ReportHub.Log(ReportCategory.MULTIPLAYER, "Attempting reconnection...");

                                await Task.Delay(RECONNECTION_DELAY_MS, parentCt);

                                try { await ConnectAsync(parentCt); }
                                catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, ReportCategory.MULTIPLAYER); }
                            }

                            break;
                        }

                        if (!evt.IsMessage(out IncomingMessage message)) continue;

                        try
                        {
                            if (syncHandlers.TryGetValue(message.Message.MessageCase, out Action<IncomingMessage>? handler))
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

        private string BuildAuthChain()
        {
            authChainBuffer.Clear();

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using AuthChain authChain = identityCache.EnsuredIdentity().Sign($"connect:/:{timestamp}:{{}}");
            var authChainIndex = 0;

            foreach (AuthLink link in authChain)
            {
                authChainBuffer[$"x-identity-auth-chain-{authChainIndex}"] = link.ToJson();
                authChainIndex++;
            }

            authChainBuffer["x-identity-timestamp"] = timestamp.ToString();
            authChainBuffer["x-identity-metadata"] = "{}";

            return JsonConvert.SerializeObject(authChainBuffer);
        }
    }
}
