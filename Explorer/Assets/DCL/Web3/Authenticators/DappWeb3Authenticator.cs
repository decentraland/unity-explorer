using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
    {
        private const int TIMEOUT_SECONDS = 30;
        private const int RPC_BUFFER_SIZE = 50000;
        private const string NETWORK_MAINNET = "mainnet";
        private const string NETWORK_SEPOLIA = "sepolia";
        private const string MAINNET_CHAIN_ID = "0x1";
        private const string SEPOLIA_CHAIN_ID = "0xaa36a7";
        private const string MAINNET_NET_VERSION = "1";
        private const string SEPOLIA_NET_VERSION = "11155111";

        private readonly IWebBrowser webBrowser;
        private readonly URLAddress authApiUrl;
        private readonly URLAddress signatureWebAppUrl;
        private readonly URLDomain rpcServerUrl;
        private readonly IWeb3IdentityCache identityCache;
        private readonly IWeb3AccountFactory web3AccountFactory;
        private readonly HashSet<string> whitelistMethods;
        private readonly HashSet<string> readOnlyMethods;
        private readonly DecentralandEnvironment environment;
        private readonly int? identityExpirationDuration;

        // Allow only one web3 operation at a time
        private readonly SemaphoreSlim mutex = new (1, 1);
        private readonly byte[] rpcByteBuffer = new byte[RPC_BUFFER_SIZE];
        private readonly URLBuilder urlBuilder = new ();

        private int authApiPendingOperations;
        private int rpcPendingOperations;
        private SocketIO? authApiWebSocket;
        private ClientWebSocket? rpcWebSocket;
        private UniTaskCompletionSource<SocketIOResponse>? signatureOutcomeTask;
        private IWeb3VerifiedAuthenticator.VerificationDelegate? loginVerificationCallback;
        private IVerifiedEthereumApi.VerificationDelegate? signatureVerificationCallback;

        public DappWeb3Authenticator(IWebBrowser webBrowser,
            URLAddress authApiUrl,
            URLAddress signatureWebAppUrl,
            URLDomain rpcServerUrl,
            IWeb3IdentityCache identityCache,
            IWeb3AccountFactory web3AccountFactory,
            HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods,
            DecentralandEnvironment environment,
            int? identityExpirationDuration = null)
        {
            this.webBrowser = webBrowser;
            this.authApiUrl = authApiUrl;
            this.signatureWebAppUrl = signatureWebAppUrl;
            this.rpcServerUrl = rpcServerUrl;
            this.identityCache = identityCache;
            this.web3AccountFactory = web3AccountFactory;
            this.whitelistMethods = whitelistMethods;
            this.readOnlyMethods = readOnlyMethods;
            this.environment = environment;
            this.identityExpirationDuration = identityExpirationDuration;
        }

        public void Dispose()
        {
            try { authApiWebSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }

        public async UniTask<EthApiResponse> SendAsync(EthApiRequest request, CancellationToken ct)
        {
            if (!whitelistMethods.Contains(request.method))
                throw new Web3Exception($"The method is not allowed: {request.method}");

            if (string.Equals(request.method, "eth_accounts")
                || string.Equals(request.method, "eth_requestAccounts"))
            {
                string[] accounts = Array.Empty<string>();

                if (identityCache.Identity != null)
                    accounts = new string[] { identityCache.EnsuredIdentity().Address };

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = accounts,
                };
            }

            if (string.Equals(request.method, "eth_chainId"))
            {
                string chainId = GetChainId();

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = chainId,
                };
            }

            if (string.Equals(request.method, "net_version"))
            {
                string netVersion = GetNetVersion();

                return new EthApiResponse
                {
                    id = request.id,
                    jsonrpc = "2.0",
                    result = netVersion,
                };
            }

            if (IsReadOnly(request))
                return await SendWithoutConfirmationAsync(request, ct);

            return await SendWithConfirmationAsync(request, ct);
        }

        /// <summary>
        ///     1. An authentication request is sent to the server
        ///     2. Open a tab to let the user sign through the browser with his custom installed wallet
        ///     3. Use the signature information to generate the identity
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="Web3Exception"></exception>
        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            await mutex.WaitAsync(ct);

            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                await UniTask.SwitchToMainThread(ct);

                await ConnectToAuthApiAsync();

                var ephemeralAccount = web3AccountFactory.CreateRandomAccount();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

                SignatureIdResponse authenticationResponse = await RequestEthMethodWithSignatureAsync(new LoginAuthApiRequest
                {
                    method = "dcl_personal_sign",
                    @params = new object[] { ephemeralMessage },
                }, ct);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(ct);

                loginVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration, authenticationResponse.requestId);

                LoginAuthApiResponse response = await RequestWalletConfirmationAsync<LoginAuthApiResponse>(authenticationResponse.requestId,
                    signatureExpiration, ct);

                await DisconnectFromAuthApiAsync();

                if (string.IsNullOrEmpty(response.sender))
                    throw new Web3Exception($"Cannot solve the signer's address from the signature. Request id: {authenticationResponse.requestId}");

                if (string.IsNullOrEmpty(response.result))
                    throw new Web3Exception($"Cannot solve the signature. Request id: {authenticationResponse.requestId}");

                AuthChain authChain = CreateAuthChain(response, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                return new DecentralandIdentity(new Web3Address(response.sender),
                    ephemeralAccount, sessionExpiration, authChain);
            }
            catch (Exception)
            {
                await DisconnectFromAuthApiAsync();
                throw;
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);

                mutex.Release();
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await DisconnectFromAuthApiAsync();

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
            loginVerificationCallback = callback;

        public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback) =>
            signatureVerificationCallback = callback;

        private async UniTask DisconnectFromAuthApiAsync()
        {
            if (authApiWebSocket is { Connected: true })
                await authApiWebSocket.DisconnectAsync();
        }

        private async UniTask<EthApiResponse> SendWithoutConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                rpcPendingOperations++;

                await mutex.WaitAsync(ct);

                await UniTask.SwitchToMainThread(ct);

                await ConnectToRpcAsync(GetNetworkId(), ct);

                var response = await RequestEthMethodWithoutSignatureAsync(request, ct)
                   .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

                if (rpcPendingOperations <= 1)
                    await DisconnectFromRpcAsync(ct);

                return response;
            }
            catch (Exception)
            {
                await DisconnectFromRpcAsync(ct);
                throw;
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, ct);
                else
                    await UniTask.SwitchToMainThread(ct);

                mutex.Release();
                rpcPendingOperations--;
            }
        }

        private async UniTask DisconnectFromRpcAsync(CancellationToken ct)
        {
            if (rpcWebSocket == null) return;

            await rpcWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
            rpcWebSocket.Abort();
            rpcWebSocket.Dispose();
            rpcWebSocket = null;
        }

        private async UniTask ConnectToRpcAsync(string network, CancellationToken ct)
        {
            if (rpcWebSocket?.State == WebSocketState.Open) return;

            urlBuilder.Clear();
            urlBuilder.AppendDomain(rpcServerUrl);
            urlBuilder.AppendPath(new URLPath(network));

            rpcWebSocket = new ClientWebSocket();
            await rpcWebSocket.ConnectAsync(new Uri(urlBuilder.Build()), ct);
        }

        private async UniTask<EthApiResponse> RequestEthMethodWithoutSignatureAsync(EthApiRequest request, CancellationToken ct)
        {
            string reqJson = JsonConvert.SerializeObject(request);
            byte[] bytes = Encoding.UTF8.GetBytes(reqJson);
            await rpcWebSocket!.SendAsync(bytes, WebSocketMessageType.Text, true, ct);

            while (!ct.IsCancellationRequested && rpcWebSocket?.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result = await rpcWebSocket.ReceiveAsync(rpcByteBuffer, ct);

                if (result.MessageType is WebSocketMessageType.Text or WebSocketMessageType.Binary)
                {
                    string resJson = Encoding.UTF8.GetString(rpcByteBuffer, 0, result.Count);
                    EthApiResponse response = JsonConvert.DeserializeObject<EthApiResponse>(resJson);

                    if (response.id == request.id)
                        return response;
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await DisconnectFromRpcAsync(ct);
                    break;
                }
            }

            throw new Web3Exception("Unexpected data received from rpc");
        }

        private async UniTask<EthApiResponse> SendWithConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
            SynchronizationContext originalSyncContext = SynchronizationContext.Current;

            try
            {
                authApiPendingOperations++;

                await mutex.WaitAsync(ct);

                await UniTask.SwitchToMainThread(ct);

                await ConnectToAuthApiAsync();

                SignatureIdResponse authenticationResponse = await RequestEthMethodWithSignatureAsync(new AuthorizedEthApiRequest
                {
                    method = request.method,
                    @params = request.@params,
                    authChain = identityCache.Identity!.AuthChain.ToArray(),
                }, ct);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(ct);

                signatureVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration);

                MethodResponse response = await RequestWalletConfirmationAsync<MethodResponse>(authenticationResponse.requestId, signatureExpiration, ct);

                if (authApiPendingOperations <= 1)
                    await DisconnectFromAuthApiAsync();

                // Strip out the requestId & sender fields. We assume that will not be needed by the client
                return new EthApiResponse
                {
                    id = request.id,
                    result = response.result,
                    jsonrpc = "2.0",
                };
            }
            catch (Exception)
            {
                await DisconnectFromAuthApiAsync();
                throw;
            }
            finally
            {
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, ct);
                else
                    await UniTask.SwitchToMainThread(ct);

                mutex.Release();
                authApiPendingOperations--;
            }
        }

        private AuthChain CreateAuthChain(LoginAuthApiResponse response, string ephemeralMessage)
        {
            var authChain = AuthChain.Create();

            // To keep cohesiveness between the platform, convert the user address to lower case
            authChain.SetSigner(response.sender.ToLower());

            string signature = response.result;

            AuthLinkType ecdsaType = signature.Length == 132
                ? AuthLinkType.ECDSA_EPHEMERAL
                : AuthLinkType.ECDSA_EIP_1654_EPHEMERAL;

            authChain.Set(new AuthLink
            {
                type = ecdsaType,
                payload = ephemeralMessage,
                signature = signature,
            });

            return authChain;
        }

        private string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address.OriginalFormat}\nExpiration: {expiration:yyyy-MM-ddTHH:mm:ss.fffZ}";

        private void ProcessSignatureOutcomeMessage(SocketIOResponse response) =>
            signatureOutcomeTask?.TrySetResult(response);

        private async UniTask<T> RequestWalletConfirmationAsync<T>(string requestId, DateTime expiration, CancellationToken ct)
        {
            webBrowser.OpenUrl($"{signatureWebAppUrl}/{requestId}");

            signatureOutcomeTask?.TrySetCanceled(ct);
            signatureOutcomeTask = new UniTaskCompletionSource<SocketIOResponse>();

            TimeSpan duration = expiration - DateTime.UtcNow;

            try
            {
                SocketIOResponse response = await signatureOutcomeTask.Task.Timeout(duration).AttachExternalCancellation(ct);
                return response.GetValue<T>();
            }
            catch (TimeoutException) { throw new SignatureExpiredException(expiration); }
        }

        private async UniTask<SignatureIdResponse> RequestEthMethodWithSignatureAsync(
            object request,
            CancellationToken ct)
        {
            UniTaskCompletionSource<SignatureIdResponse> task = new ();

            await authApiWebSocket!.EmitAsync("request", ct,
                r =>
                {
                    SignatureIdResponse signatureIdResponse = r.GetValue<SignatureIdResponse>();

                    if (!string.IsNullOrEmpty(signatureIdResponse.error))
                        task.TrySetException(new Web3Exception(signatureIdResponse.error));
                    else if (string.IsNullOrEmpty(signatureIdResponse.requestId))
                        task.TrySetException(new Web3Exception("Cannot solve auth request id"));
                    else
                        task.TrySetResult(signatureIdResponse);
                }, request);

            return await task.Task.Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
                             .AttachExternalCancellation(ct);
        }

        private async UniTask ConnectToAuthApiAsync()
        {
            if (authApiWebSocket == null)
            {
                var uri = new Uri(authApiUrl);

                authApiWebSocket = new SocketIO(uri, new SocketIOOptions
                {
                    Transport = TransportProtocol.WebSocket,
                });

                authApiWebSocket.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings());

                authApiWebSocket.On("outcome", ProcessSignatureOutcomeMessage);
            }

            if (authApiWebSocket.Connected) return;

            await authApiWebSocket
                 .ConnectAsync()
                 .AsUniTask()
                 .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
        }

        private bool IsReadOnly(EthApiRequest request)
        {
            foreach (string method in readOnlyMethods)
                if (string.Equals(method, request.method, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private string GetNetVersion() =>
            // TODO: this is a temporary thing until we solve the network in a better way
            environment == DecentralandEnvironment.Org ? MAINNET_NET_VERSION : SEPOLIA_NET_VERSION;

        private string GetChainId() =>
            // TODO: this is a temporary thing until we solve the network in a better way
            environment == DecentralandEnvironment.Org ? MAINNET_CHAIN_ID : SEPOLIA_CHAIN_ID;

        private string GetNetworkId() =>
            // TODO: this is a temporary thing until we solve the network in a better way (probably it should be parametrized)
            environment == DecentralandEnvironment.Org ? NETWORK_MAINNET : NETWORK_SEPOLIA;
    }
}
