using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Abstract;
using DCL.Web3.Chains;
using DCL.Web3.Identities;
using Newtonsoft.Json;

///*
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
//*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Web3.Authenticators
{
    internal delegate void VerificationWeb3Delegate(int code, DateTime expiration);

    public partial class DappWeb3Authenticator : IWeb3VerifiedAuthenticator, IEthereumApi
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
        private readonly ICodeVerificationFeatureFlag codeVerificationFeatureFlag;
        private readonly int? identityExpirationDuration;

        // Allow only one web3 operation at a time
        private readonly DCLSemaphoreSlim mutex = new (1, 1);
        private readonly byte[] rpcByteBuffer = new byte[RPC_BUFFER_SIZE];
        private readonly URLBuilder urlBuilder = new ();

        private int authApiPendingOperations;
        private int rpcPendingOperations;
        private SocketIO? authApiWebSocket;
        private ClientWebSocket? rpcWebSocket;
        private UniTaskCompletionSource<SocketIOResponse>? signatureOutcomeTask;
        private UniTaskCompletionSource<SocketIOResponse>? codeVerificationTask;
        private IWeb3VerifiedAuthenticator.VerificationDelegate? codeVerificationCallback;
        private VerificationWeb3Delegate? signatureVerificationCallback;

        //private 
        public    DappWeb3Authenticator(IWebBrowser webBrowser,
            URLAddress authApiUrl,
            URLAddress signatureWebAppUrl,
            URLDomain rpcServerUrl,
            IWeb3IdentityCache identityCache,
            IWeb3AccountFactory web3AccountFactory,
            HashSet<string> whitelistMethods,
            HashSet<string> readOnlyMethods,
            DecentralandEnvironment environment,
            ICodeVerificationFeatureFlag codeVerificationFeatureFlag,
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
            this.codeVerificationFeatureFlag = codeVerificationFeatureFlag;
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
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:152"); // SPECIAL_DEBUG_LINE_STATEMENT
            await mutex.WaitAsync(ct);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:155"); // SPECIAL_DEBUG_LINE_STATEMENT
#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:160"); // SPECIAL_DEBUG_LINE_STATEMENT
            try
            {
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:163"); // SPECIAL_DEBUG_LINE_STATEMENT
                await UniTask.SwitchToMainThread(ct);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:166"); // SPECIAL_DEBUG_LINE_STATEMENT
                await ConnectToAuthApiAsync();

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:169"); // SPECIAL_DEBUG_LINE_STATEMENT
                var ephemeralAccount = web3AccountFactory.CreateRandomAccount();

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:172"); // SPECIAL_DEBUG_LINE_STATEMENT
                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = identityExpirationDuration != null
                    ? DateTime.UtcNow.AddSeconds(identityExpirationDuration.Value)
                    : DateTime.UtcNow.AddDays(7);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:178"); // SPECIAL_DEBUG_LINE_STATEMENT
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:181"); // SPECIAL_DEBUG_LINE_STATEMENT
                SignatureIdResponse authenticationResponse = await RequestEthMethodWithSignatureAsync(new LoginAuthApiRequest
                {
                    method = "dcl_personal_sign",
                    @params = new object[] { ephemeralMessage },
                }, ct);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:188"); // SPECIAL_DEBUG_LINE_STATEMENT
                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:191"); // SPECIAL_DEBUG_LINE_STATEMENT
                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:195"); // SPECIAL_DEBUG_LINE_STATEMENT
                await UniTask.SwitchToMainThread(ct);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:198"); // SPECIAL_DEBUG_LINE_STATEMENT
                if (codeVerificationFeatureFlag.ShouldWaitForCodeVerificationFromServer)
                    WaitForCodeVerificationAsync(authenticationResponse.requestId, authenticationResponse.code, signatureExpiration, ct).Forget();
                else
                    codeVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration, authenticationResponse.requestId);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:204"); // SPECIAL_DEBUG_LINE_STATEMENT
                LoginAuthApiResponse response = await RequestSignatureAsync<LoginAuthApiResponse>(authenticationResponse.requestId,
                    signatureExpiration, ct);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:208"); // SPECIAL_DEBUG_LINE_STATEMENT
                await DisconnectFromAuthApiAsync();

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:211"); // SPECIAL_DEBUG_LINE_STATEMENT
                if (string.IsNullOrEmpty(response.sender))
                    throw new Web3Exception($"Cannot solve the signer's address from the signature. Request id: {authenticationResponse.requestId}");

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:215"); // SPECIAL_DEBUG_LINE_STATEMENT
                if (string.IsNullOrEmpty(response.result))
                    throw new Web3Exception($"Cannot solve the signature. Request id: {authenticationResponse.requestId}");

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:219"); // SPECIAL_DEBUG_LINE_STATEMENT
                AuthChain authChain = CreateAuthChain(response, ephemeralMessage);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:222"); // SPECIAL_DEBUG_LINE_STATEMENT
                // To keep cohesiveness between the platform, convert the user address to lower case
                return new DecentralandIdentity(new Web3Address(response.sender),
                    ephemeralAccount, sessionExpiration, authChain);
            }
            catch (Exception)
            {
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:229"); // SPECIAL_DEBUG_LINE_STATEMENT
                await DisconnectFromAuthApiAsync();
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:231"); // SPECIAL_DEBUG_LINE_STATEMENT
                throw;
            }
            finally
           {
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:236"); // SPECIAL_DEBUG_LINE_STATEMENT
// There is no need for switching the threads on WebGL
#if !UNITY_WEBGL
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:239"); // SPECIAL_DEBUG_LINE_STATEMENT
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext, CancellationToken.None);
                else
                    await UniTask.SwitchToMainThread(CancellationToken.None);
#endif

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:246"); // SPECIAL_DEBUG_LINE_STATEMENT
                mutex.Release();
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:248"); // SPECIAL_DEBUG_LINE_STATEMENT
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken) =>
            await DisconnectFromAuthApiAsync();

        public void CancelCurrentWeb3Operation()
        {
            // Cancel the task waiting for the browser signature
            signatureOutcomeTask?.TrySetCanceled();

            // Also cancel code verification if that's what was hanging (during Login)
            codeVerificationTask?.TrySetCanceled();
        }
        
        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
            codeVerificationCallback = callback;

        private void AddVerificationListener(VerificationWeb3Delegate callback) =>
            signatureVerificationCallback = callback;

        private async UniTask DisconnectFromAuthApiAsync()
        {
            if (authApiWebSocket is { Connected: true })
                await authApiWebSocket.DisconnectAsync();

            codeVerificationTask?.TrySetCanceled();
            signatureOutcomeTask?.TrySetCanceled();
        }

        private async UniTask<EthApiResponse> SendWithoutConfirmationAsync(EthApiRequest request, CancellationToken ct)
        {
#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
#endif

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
// There is no need for switching the threads on WebGL
#if !UNITY_WEBGL
                // CRITICAL: Do not pass the CancellationToken (ct) to these switches.
                // If the token is cancelled, the await will throw an OperationCanceledException immediately.
                // This would abort the 'finally' block before reaching mutex.Release(), causing a permanent deadlock.
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext);
                else
                    await UniTask.SwitchToMainThread();
#endif

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
#if !UNITY_WEBGL
            SynchronizationContext originalSyncContext = SynchronizationContext.Current; // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG  
#endif

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

                MethodResponse response = await RequestSignatureAsync<MethodResponse>(authenticationResponse.requestId, signatureExpiration, ct);

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
// There is no need for switching the threads on WebGL
#if !UNITY_WEBGL
                // CRITICAL: Do not pass the CancellationToken (ct) to these switches.
                // If the token is cancelled, the await will throw an OperationCanceledException immediately.
                // This would abort the 'finally' block before reaching mutex.Release(), causing a permanent deadlock.
                if (originalSyncContext != null)
                    await UniTask.SwitchToSynchronizationContext(originalSyncContext);
                else
                    await UniTask.SwitchToMainThread();
#endif

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

        private void ProcessCodeVerificationStatus(SocketIOResponse response) =>
            codeVerificationTask?.TrySetResult(response);

        private async UniTask<T> RequestSignatureAsync<T>(string requestId, DateTime expiration, CancellationToken ct)
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
            catch (WebSocketException e) { throw new Web3SignatureException("An error occurred while requesting signature: unable to complete the operation due to a WebSocket issue", e); }
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
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:519"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (authApiWebSocket == null)
            {
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:522"); // SPECIAL_DEBUG_LINE_STATEMENT
                var uri = new Uri(authApiUrl);

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:525"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket = new SocketIO(uri, new SocketIOOptions
                {
                    Transport = TransportProtocol.WebSocket,
                });

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:531"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings());

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:534"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket.On("outcome", ProcessSignatureOutcomeMessage);
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:536"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket.On("request-validation-status", ProcessCodeVerificationStatus);
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:538"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket.OnDisconnected += OnWebSocketDisconnected;
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:540"); // SPECIAL_DEBUG_LINE_STATEMENT
                authApiWebSocket.OnError += OnWebSocketError;
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:542"); // SPECIAL_DEBUG_LINE_STATEMENT
            }

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:545"); // SPECIAL_DEBUG_LINE_STATEMENT
            if (authApiWebSocket.Connected) return;

UnityEngine.Debug.Log("DappWeb3Authenticator.cs:548"); // SPECIAL_DEBUG_LINE_STATEMENT
            await authApiWebSocket
                 .ConnectAsync()
                 .AsUniTask()
                 .Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
UnityEngine.Debug.Log("DappWeb3Authenticator.cs:553"); // SPECIAL_DEBUG_LINE_STATEMENT
        }

        private void OnWebSocketError(object sender, string error)
        {
            signatureOutcomeTask?.TrySetException(new WebSocketException(WebSocketError.Faulted, error));
            codeVerificationTask?.TrySetException(new WebSocketException(WebSocketError.Faulted, error));
        }

        private void OnWebSocketDisconnected(object sender, string reason)
        {
            signatureOutcomeTask?.TrySetException(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, reason));
            codeVerificationTask?.TrySetException(new WebSocketException(WebSocketError.ConnectionClosedPrematurely, reason));
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
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? MAINNET_NET_VERSION : SEPOLIA_NET_VERSION;

        private string GetChainId() =>
            // TODO: this is a temporary thing until we solve the network in a better way
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? MAINNET_CHAIN_ID : SEPOLIA_CHAIN_ID;

        private string GetNetworkId() =>
            // TODO: this is a temporary thing until we solve the network in a better way (probably it should be parametrized)
            environment is DecentralandEnvironment.Org or DecentralandEnvironment.Today ? NETWORK_MAINNET : NETWORK_SEPOLIA;

        /// <summary>
        /// Waits until we receive the verification status from the server
        /// So then we execute the loginVerificationCallback
        /// </summary>
        private async UniTask WaitForCodeVerificationAsync(string requestId, int code, DateTime expiration, CancellationToken ct)
        {
            codeVerificationTask?.TrySetCanceled(ct);
            codeVerificationTask = new UniTaskCompletionSource<SocketIOResponse>();

            TimeSpan duration = expiration - DateTime.UtcNow;

            try
            {
                SocketIOResponse response = await codeVerificationTask.Task.Timeout(duration).AttachExternalCancellation(ct);

                var validation = response.GetValue<CodeVerificationStatus>();

                if (validation.requestId == requestId)
                {
                    await UniTask.SwitchToMainThread(ct);
                    codeVerificationCallback?.Invoke(code, expiration, requestId);
                }
            }
            catch (TimeoutException e) { throw new CodeVerificationException($"Code verification expired: {expiration}", e); }
            catch (WebSocketException e) { throw new CodeVerificationException("An error occurred while verifying the code: unable to complete the operation due to a WebSocket issue", e); }
        }
    }
}
