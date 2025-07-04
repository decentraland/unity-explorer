using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using rpc_csharp;
using Sentry;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using Utility;
using RpcClient = rpc_csharp.RpcClient;

namespace DCL.SocialService
{
    public interface IRPCSocialServices : IDisposable
    {
        /// <summary>
        ///     Check if the service is currently connected and ready to handle requests.
        /// </summary>
        bool IsConnected { get; }

        RpcClientModule Module();

        /// <summary>
        ///     Try to establish connection to the RPC server until the cancellation token is triggered or the retries count is exhausted.
        /// </summary>
        UniTask EnsureRpcConnectionAsync(int connectionRetries, CancellationToken ct);

        /// <summary>
        ///     Subscribe to connection management. Returns a subscription that should be disposed when no longer needed.
        ///     Multiple subscriptions will share the same underlying connection.
        /// </summary>
        ConnectionSubscription SubscribeToConnection(CancellationToken ct);

        /// <summary>
        ///     Start connection management. Will automatically disconnect after 30 seconds if no subscribers join.
        /// </summary>
        void StartConnectionManagement();

        /// <summary>
        ///     Stop connection management.
        /// </summary>
        void StopConnectionManagement();

        /// <summary>
        ///     Unsubscribe from connection management. This is called internally by ConnectionSubscription.Dispose().
        /// </summary>
        void Unsubscribe(ConnectionSubscription subscription);
    }

    public class RPCSocialServices : IRPCSocialServices
    {
        private const string RPC_PORT_NAME = "social_service";
        private const string RPC_SERVICE_NAME = "SocialService";

        private const int CONNECTION_TIMEOUT_SECS = 10;
        private const int FOREGROUND_CONNECTION_RETRIES = int.MaxValue;

        private const double RETRY_BACKOFF_DELAY_MIN = 1.0;
        private const double RETRY_BACKOFF_DELAY_MAX = 45.0;

        private const double RETRY_BACKOFF_MULTIPLIER = 2.0;

        /// <summary>
        ///     Used to ensure that only one connection establishment process is running at a time.
        /// </summary>
        private readonly SemaphoreSlim connectionEstablishingMutex = new (1, 1);

        /// <summary>
        ///     Used to ensure that handshake and disconnection processes do not overlap.
        /// </summary>
        private readonly SemaphoreSlim handshakeMutex = new (1, 1);

        private readonly URLAddress apiUrl;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<string, string> authChainBuffer = new ();
        private readonly ISocialServiceEventBus socialServiceEventBus;

        private readonly List<ConnectionSubscription> activeSubscriptions = new ();

        private double retryCurrentDelay = RETRY_BACKOFF_DELAY_MIN;

        private RpcClientModule? module;
        private RpcClientPort? port;
        private WebSocketRpcTransport? transport;
        private RpcClient? client;
        private CancellationTokenSource? connectionCts;
        private bool isConnectionManagementActive;
        private int consecutiveFailures = 0;
        private const int MAX_CONSECUTIVE_FAILURES = 10;
        private const int CONNECTION_MANAGEMENT_TIMEOUT_SECONDS = 30;
        private static readonly TimeSpan CONNECTION_MANAGEMENT_TIMEOUT = TimeSpan.FromSeconds(CONNECTION_MANAGEMENT_TIMEOUT_SECONDS);

        public bool IsConnected => isConnectionReady;

        private bool isConnectionReady => transport?.State == WebSocketState.Open
                                          && module != null
                                          && client != null
                                          && port != null;

        public RPCSocialServices(
            URLAddress apiUrl,
            IWeb3IdentityCache identityCache,
            ISocialServiceEventBus socialServiceEventBus)
        {
            this.apiUrl = apiUrl;
            this.identityCache = identityCache;
            this.socialServiceEventBus = socialServiceEventBus;
        }

        public void Dispose()
        {
            StopConnectionManagement();
            transport?.Dispose();
            client?.Dispose();
            connectionEstablishingMutex.Dispose();
            handshakeMutex.Dispose();
            authChainBuffer.Clear();
        }

        public RpcClientModule Module() =>
            module;

        public ConnectionSubscription SubscribeToConnection(CancellationToken ct)
        {
            var subscription = new ConnectionSubscription(this, ct);

            lock (activeSubscriptions) { activeSubscriptions.Add(subscription); }

            // Start connection management if not already active
            if (!isConnectionManagementActive)
            {
                StartConnectionManagement();
            }

            return subscription;
        }

        public void StartConnectionManagement()
        {
            lock (activeSubscriptions)
            {
                if (isConnectionManagementActive)
                    return;

                isConnectionManagementActive = true;
                consecutiveFailures = 0; // Reset failure counter on new connection attempt
                connectionCts = new CancellationTokenSource();
                ManageConnectionAsync(CONNECTION_MANAGEMENT_TIMEOUT, connectionCts.Token).Forget();
            }
        }

        public void StopConnectionManagement()
        {
            lock (activeSubscriptions)
            {
                if (!isConnectionManagementActive)
                    return;

                isConnectionManagementActive = false;
                connectionCts?.SafeCancelAndDispose();
                connectionCts = null;
            }
        }

        private async UniTaskVoid ManageConnectionAsync(TimeSpan timeout, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Check if we have any active subscriptions
                    lock (activeSubscriptions)
                    {
                        if (activeSubscriptions.Count == 0)
                        {
                            isConnectionManagementActive = false;
                            return;
                        }
                    }

                    try
                    {
                        // Try to establish connection
                        await EnsureRpcConnectionAsync(FOREGROUND_CONNECTION_RETRIES, ct);

                        // Connection successful - reset failure counter
                        consecutiveFailures = 0;

                        // Notify all subscriptions that connection is ready
                        lock (activeSubscriptions)
                        {
                            foreach (ConnectionSubscription subscription in activeSubscriptions.ToArray()) { subscription.NotifyConnected(); }
                        }

                        // Wait for connection to be lost or cancellation
                        while (isConnectionReady && !ct.IsCancellationRequested) { await UniTask.Delay(1000, cancellationToken: ct); }

                        // Notify all subscriptions that connection is lost
                        lock (activeSubscriptions)
                        {
                            foreach (ConnectionSubscription subscription in activeSubscriptions.ToArray()) { subscription.NotifyDisconnected(); }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception e)
                    {
                        consecutiveFailures++;
                        ReportHub.LogError(ReportCategory.ENGINE, $"RPC connection failed (attempt {consecutiveFailures}/{MAX_CONSECUTIVE_FAILURES}): {e.Message}");

                        // Check if we've exceeded the maximum consecutive failures
                        if (consecutiveFailures >= MAX_CONSECUTIVE_FAILURES)
                        {
                            // Permanent failure - notify all subscriptions and stop retrying
                            ReportHub.LogError(ReportCategory.ENGINE, $"RPC connection permanently failed after {MAX_CONSECUTIVE_FAILURES} consecutive attempts");

                            lock (activeSubscriptions)
                            {
                                foreach (ConnectionSubscription subscription in activeSubscriptions.ToArray()) { subscription.NotifyConnectionFailed(); }
                            }

                            // Stop connection management permanently
                            isConnectionManagementActive = false;
                            return;
                        }

                        // Check if we still have any active subscriptions
                        lock (activeSubscriptions)
                        {
                            if (activeSubscriptions.Count == 0)
                            {
                                isConnectionManagementActive = false;
                                return; // Exit cleanly if no more subscriptions
                            }
                        }

                        // Wait before retrying the entire connection management cycle
                        try { await UniTask.Delay(TimeSpan.FromSeconds(30), cancellationToken: ct); }
                        catch (OperationCanceledException) { break; }
                    }
                }
            }
            finally
            {
                lock (activeSubscriptions) { isConnectionManagementActive = false; }
            }
        }

        public void Unsubscribe(ConnectionSubscription subscription)
        {
            lock (activeSubscriptions)
            {
                activeSubscriptions.Remove(subscription);

                // Stop connection management if no more subscriptions
                if (activeSubscriptions.Count == 0) { StopConnectionManagement(); }
            }
        }

        public async UniTask DisconnectAsync(CancellationToken ct)
        {
            try
            {
                await handshakeMutex.WaitAsync(ct);

                port?.Close();
                port = null;
                module = null;

                if (transport != null)
                {
                    await transport.CloseAsync(ct);
                    transport.OnCloseEvent -= OnTransportClosed;
                    transport.Dispose();
                    transport = null;
                }

                client?.Dispose();
                client = null;
            }
            finally { handshakeMutex.Release(); }
        }

        public async UniTask EnsureRpcConnectionAsync(int connectionRetries, CancellationToken ct)
        {
            const string BREADCRUMB_CATEGORY = "RPC Service";

            // Ensuring runs in the infinite loop,
            // but it's bound to the cancellation token originated from the source of the procedure invocation.
            // if the source of invocation goes out of scope the next request will take over the ensuring/reconnection process

            var mutexAcquired = false;

            try
            {
                // by acquiring the mutex while the whole loop is running
                // prevent ping-ponging between different methods in competition for waiting for the mutex availability
                await connectionEstablishingMutex.WaitAsync(ct);
                mutexAcquired = true;

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        connectionRetries--;
                        await StartHandshakeAsync(ct);

                        SentrySdk.AddBreadcrumb("Connection established successfully", category: BREADCRUMB_CATEGORY, level: BreadcrumbLevel.Info);

                        // Reset the retry delay after a successful connection
                        retryCurrentDelay = RETRY_BACKOFF_DELAY_MIN;

                        // Return on success
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        if (connectionRetries > 0)
                        {
                            // Add a breadcrumb to better investigate the issue
                            SentrySdk.AddBreadcrumb(ex.Message, category: BREADCRUMB_CATEGORY, level: BreadcrumbLevel.Error);

                            double appliedDelay = retryCurrentDelay;

                            // Preserved the delay in case this process is cancelled
                            retryCurrentDelay = Math.Min(retryCurrentDelay * RETRY_BACKOFF_MULTIPLIER, RETRY_BACKOFF_DELAY_MAX);

                            await UniTask.Delay(TimeSpan.FromSeconds(appliedDelay), DelayType.UnscaledDeltaTime, cancellationToken: ct);
                        }
                        else
                        {
                            // If we reach here, it means we exhausted the retries and failed to connect
                            throw new WebSocketException($"Failed to connect after {connectionRetries} attempts", ex);
                        }
                    }
                }
            }
            finally
            {
                if (mutexAcquired)
                    connectionEstablishingMutex.Release();
            }
        }

        private async UniTask StartHandshakeAsync(CancellationToken ct)
        {
            var acquired = false;

            try
            {
                await handshakeMutex.WaitAsync(ct);
                acquired = true;

                if (!isConnectionReady)
                    await InitializeConnectionAsync(ct);
            }
            catch (Exception)
            {
                // If we get here, the connection isn't ready and we should clean up

                port?.Close();
                port = null;
                module = null;
                transport?.Dispose();
                transport = null;
                client?.Dispose();
                client = null;

                throw;
            }
            finally
            {
                if (acquired)
                    handshakeMutex.Release();
            }
        }

        private async UniTask InitializeConnectionAsync(CancellationToken ct)
        {
            client?.Dispose();
            transport?.Dispose();

            transport = new WebSocketRpcTransport(new Uri(apiUrl));
            transport.OnCloseEvent += OnTransportClosed;
            client = new RpcClient(transport);

            await transport.ConnectAsync(ct).Timeout(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECS));

            string authChain = BuildAuthChain();

            // The service expects the auth-chain in json format within a 30 seconds threshold after connection
            await transport.SendMessageAsync(authChain, ct);

            transport.ListenForIncomingData();

            port = await client.CreatePort(RPC_PORT_NAME);
            module = await port!.LoadModule(RPC_SERVICE_NAME);

            socialServiceEventBus.SendWebSocketConnectionEstablishedNotification();
        }

        private string BuildAuthChain()
        {
            authChainBuffer.Clear();

            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            using AuthChain authChain = identityCache.EnsuredIdentity().Sign($"get:/:{timestamp}:{{}}");
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

        private void OnTransportClosed() =>
            socialServiceEventBus.SendTransportClosedNotification();
    }
}
