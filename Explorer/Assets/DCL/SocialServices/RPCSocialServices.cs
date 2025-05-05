using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using rpc_csharp;
using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using UnityEngine;
using RpcClient = rpc_csharp.RpcClient;

namespace DCL.SocialService
{
    public interface IRPCSocialServices : IDisposable
    {
        public RpcClientModule Module();

        public UniTask EnsureRpcConnectionAsync(CancellationToken ct);
    }

    public class RPCSocialServices : IRPCSocialServices
    {
        private const string RPC_PORT_NAME = "social_service";
        private const string RPC_SERVICE_NAME = "SocialService";
        private const int CONNECTION_TIMEOUT_SECS = 10;
        private const int CONNECTION_RETRIES = 10;
        private const double RETRY_BACKOFF_MULTIPLIER = 1.5;

        private readonly SemaphoreSlim handshakeMutex = new (1, 1);
        private readonly URLAddress apiUrl;
        private readonly IWeb3IdentityCache identityCache;
        private readonly Dictionary<string, string> authChainBuffer = new ();
        private readonly ISocialServiceEventBus socialServiceEventBus;

        private RpcClientModule? module;
        private RpcClientPort? port;
        private WebSocketRpcTransport? transport;
        private RpcClient? client;

        public RPCSocialServices(
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
            transport?.Dispose();
            client?.Dispose();
            handshakeMutex.Dispose();
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

        public async UniTask EnsureRpcConnectionAsync(CancellationToken ct)
        {
            var handshakeFinished = false;
            int retries = CONNECTION_RETRIES;
            var backoffDelay = 1.0;

            while (!handshakeFinished && retries > 0)
                try
                {
                    retries--;
                    await StartHandshakeAsync(ct);
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


        private async UniTask StartHandshakeAsync(CancellationToken ct, bool test = false)
        {
            bool acquired = false;
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
                if (acquired)
                {
                    port?.Close();
                    port = null;
                    module = null;
                    transport?.Dispose();
                    transport = null;
                    client?.Dispose();
                    client = null;
                }
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
