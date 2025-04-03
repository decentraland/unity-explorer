using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using rpc_csharp;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using RpcClient = rpc_csharp.RpcClient;

namespace DCL.SocialService
{
    public interface ISocialServiceRPC : IDisposable
    {
        public RpcClientModule Module();
        public UniTask EnsureRpcConnectionAsync(CancellationToken ct);

    }

    public class SocialServiceRPC : ISocialServiceRPC
    {
        private const string RPC_PORT_NAME = "social_Services";
        private const string RPC_SERVICE_NAME = "SocialService";
        private const int CONNECTION_TIMEOUT_SECS = 10;
        private const int CONNECTION_RETRIES = 3;

        private readonly SemaphoreSlim handshakeMutex = new (1, 1);
        private readonly URLAddress apiUrl;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<string, string> authChainBuffer = new ();
        private readonly ISocialServiceEventBus socialServiceEventBus;

        private RpcClientModule? module;
        private RpcClientPort? port;
        private WebSocketRpcTransport? transport;
        private RpcClient? client;

        public SocialServiceRPC(
            URLAddress apiUrl,
            IWeb3IdentityCache identityCache,
            ISocialServiceEventBus socialServiceEventBus)
        {
            this.apiUrl = apiUrl;
            this.identityCache = identityCache;
            this.socialServiceEventBus = socialServiceEventBus;
        }

        private bool isConnectionReady => transport?.State == WebSocketState.Open
                                          && module != null
                                          && client != null
                                          && port != null;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed) return;

            if (disposing)
            {
                if (transport != null)
                {
                    transport.OnCloseEvent -= OnTransportClosed;
                    transport.Dispose();
                }
                client?.Dispose();
                handshakeMutex.Dispose();
                authChainBuffer.Clear();
                authChainBuilder.Clear();
            }

            isDisposed = true;
        }

        public RpcClientModule Module() => module;

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

        public async UniTask EnsureRpcConnectionAsync(CancellationToken ct)
        {
            var handshakeFinished = false;
            int retries = CONNECTION_RETRIES;
            double backoffDelay = 1.0;

            while (!handshakeFinished && retries > 0)
            {
                try
                {
                    retries--;
                    await StartHandshakeAsync();
                    handshakeFinished = true;
                }
                catch (WebSocketException wsEx)
                {
                    if (retries == 0)
                        throw new WebSocketException($"Failed to connect after {CONNECTION_RETRIES} attempts", wsEx);
                    
                    await UniTask.Delay(TimeSpan.FromSeconds(backoffDelay), cancellationToken: ct);
                    backoffDelay *= RETRY_BACKOFF_MULTIPLIER;
                }
                catch (TimeoutException)
                {
                    if (retries == 0)
                        throw;
                    
                    await UniTask.Delay(TimeSpan.FromSeconds(backoffDelay), cancellationToken: ct);
                    backoffDelay *= RETRY_BACKOFF_MULTIPLIER;
                }
            }
        }

        private async UniTask StartHandshakeAsync()
        {
            try
            {
                await handshakeMutex.WaitAsync();

                if (!isConnectionReady)
                {
                    await InitializeConnectionAsync();
                }
            }
            finally { handshakeMutex.Release(); }
        }

        private async UniTask InitializeConnectionAsync()
        {
            client?.Dispose();
            transport?.Dispose();
            
            transport = new WebSocketRpcTransport(new Uri(apiUrl));
            transport.OnCloseEvent += OnTransportClosed;
            client = new RpcClient(transport);

            await transport.ConnectAsync().Timeout(TimeSpan.FromSeconds(CONNECTION_TIMEOUT_SECS));

            string authChain = BuildAuthChain();
            await transport.SendMessageAsync(authChain).Timeout(TimeSpan.FromSeconds(AUTH_CHAIN_TIMEOUT_SECS));

            transport.ListenForIncomingData();

            port = await client.CreatePort("friends");
            module = await port.LoadModule(RPC_SERVICE_NAME);
        }

        private string BuildAuthChain()
        {
            authChainBuffer.Clear();
            authChainBuilder.Clear();

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

        private void OnTransportClosed() => socialServiceEventBus.OnTransportClosed();
    }
}
