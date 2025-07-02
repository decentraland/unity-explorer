using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using rpc_csharp;
using Sentry;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using RpcClient = rpc_csharp.RpcClient;

namespace DCL.SocialService
{
    public interface IRPCSocialServices : IDisposable
    {
        public const int FOREGROUND_CONNECTION_RETRIES = 3;

        RpcClientModule Module();

        /// <summary>
        ///     Try to establish connection to the RPC server until the cancellation token is triggered or the retries count is exhausted.
        /// </summary>
        UniTask EnsureRpcConnectionAsync(CancellationToken ct) =>
            EnsureRpcConnectionAsync(FOREGROUND_CONNECTION_RETRIES, ct);

        UniTask EnsureRpcConnectionAsync(int connectionRetries, CancellationToken ct);
    }

    public class RPCSocialServices : IRPCSocialServices
    {
        private const string RPC_PORT_NAME = "social_service";
        private const string RPC_SERVICE_NAME = "SocialService";
        private const string BREADCRUMB_CATEGORY = "RPC Service";

        private const int CONNECTION_TIMEOUT_SECS = 10;

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

        private double retryCurrentDelay = RETRY_BACKOFF_DELAY_MIN;

        private RpcClientModule? module;
        private RpcClientPort? port;
        private WebSocketRpcTransport? transport;
        private RpcClient? client;

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
            transport?.Dispose();
            client?.Dispose();
            connectionEstablishingMutex.Dispose();
            authChainBuffer.Clear();
        }

        public RpcClientModule Module() =>
            module;

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
                {
                    await InitializeConnectionAsync(ct);
                    SentrySdk.AddBreadcrumb("Connection established successfully", category: BREADCRUMB_CATEGORY, level: BreadcrumbLevel.Info);
                }
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
