using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Web3.Accounts;
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
using System.Threading;
using UnityEngine;

namespace DCL.Web3.Authenticators
{
    public partial class DappWeb3Authenticator : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
    {
        private const int TIMEOUT_SECONDS = 30;

        private readonly IWebBrowser webBrowser;
        private readonly string serverUrl;
        private readonly string signatureUrl;
        private readonly IWeb3IdentityCache identityCache;
        private readonly HashSet<string> whitelistMethods;

        private SocketIO? webSocket;
        private UniTaskCompletionSource<SocketIOResponse>? signatureOutcomeTask;
        private IWeb3VerifiedAuthenticator.VerificationDelegate? loginVerificationCallback;
        private IVerifiedEthereumApi.VerificationDelegate? signatureVerificationCallback;

        public DappWeb3Authenticator(IWebBrowser webBrowser,
            string serverUrl,
            string signatureUrl,
            IWeb3IdentityCache identityCache,
            HashSet<string> whitelistMethods)
        {
            this.webBrowser = webBrowser;
            this.serverUrl = serverUrl;
            this.signatureUrl = signatureUrl;
            this.identityCache = identityCache;
            this.whitelistMethods = whitelistMethods;
        }

        public void Dispose()
        {
            try { webSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }

        public async UniTask<T> SendAsync<T>(EthApiRequest request, CancellationToken ct)
        {
            if (!whitelistMethods.Contains(request.method))
                throw new Web3Exception($"The method is not allowed: {request.method}");

            try
            {
                await ConnectToServerAsync();

                SignatureIdResponse authenticationResponse = await RequestEthMethodAsync(new AuthorizedEthApiRequest
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

                MethodResponse<T> response = await RequestWalletConfirmationAsync<MethodResponse<T>>(authenticationResponse.requestId, signatureExpiration, ct);

                // Strip out the requestId & sender fields. We assume that will not be needed by the client
                return response.result;
            }
            finally
            {
                await DisconnectFromServerAsync();
                await UniTask.SwitchToMainThread(ct);
            }
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
            try
            {
                await ConnectToServerAsync();

                var ephemeralAccount = NethereumAccount.CreateRandom();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

                SignatureIdResponse authenticationResponse = await RequestEthMethodAsync(new EthApiRequest
                {
                    method = "dcl_personal_sign",
                    @params = new object[] { ephemeralMessage },
                }, ct);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(ct);

                loginVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration);

                LoginResponse response = await RequestWalletConfirmationAsync<LoginResponse>(authenticationResponse.requestId,
                    signatureExpiration, ct);

                if (string.IsNullOrEmpty(response.sender))
                    throw new Web3Exception($"Cannot solve the signer's address from the signature. Request id: {authenticationResponse.requestId}");

                if (string.IsNullOrEmpty(response.result))
                    throw new Web3Exception($"Cannot solve the signature. Request id: {authenticationResponse.requestId}");

                AuthChain authChain = CreateAuthChain(response, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                return new DecentralandIdentity(new Web3Address(response.sender.ToLower()),
                    ephemeralAccount, sessionExpiration, authChain);
            }
            finally
            {
                await DisconnectFromServerAsync();
                await UniTask.SwitchToMainThread(ct);
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await DisconnectFromServerAsync();
        }

        public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
            loginVerificationCallback = callback;

        public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback) =>
            signatureVerificationCallback = callback;

        private SocketIO InitializeWebSocket()
        {
            if (webSocket != null) return webSocket;

            var uri = new Uri(serverUrl);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings());

            webSocket.On("outcome", ProcessSignatureOutcomeMessage);

            return webSocket;
        }

        private async UniTask DisconnectFromServerAsync()
        {
            if (webSocket is { Connected: true })
                await webSocket.DisconnectAsync();
        }

        private AuthChain CreateAuthChain(LoginResponse response, string ephemeralMessage)
        {
            var authChain = AuthChain.Create();

            authChain.SetSigner(response.sender);

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
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";

        private async UniTask ConnectToServerAsync()
        {
            SocketIO webSocket = InitializeWebSocket();
            await webSocket.ConnectAsync().AsUniTask().Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));
        }

        private void ProcessSignatureOutcomeMessage(SocketIOResponse response)
        {
            signatureOutcomeTask?.TrySetResult(response);
        }

        private async UniTask<SignatureIdResponse> RequestEthMethodAsync(
            object request,
            CancellationToken ct)
        {
            UniTaskCompletionSource<SignatureIdResponse> task = new ();

            await webSocket!.EmitAsync("request", ct,
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

        private async UniTask<T> RequestWalletConfirmationAsync<T>(string requestId, DateTime expiration, CancellationToken ct)
        {
            webBrowser.OpenUrl($"{signatureUrl}/{requestId}");

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

        public class Default : IWeb3VerifiedAuthenticator, IVerifiedEthereumApi
        {
            private readonly IWeb3VerifiedAuthenticator originAuth;
            private readonly IVerifiedEthereumApi originApi;

            public Default(IWeb3IdentityCache identityCache)
            {
#if !UNITY_EDITOR
                string serverUrl = Debug.isDebugBuild
                    ? "https://auth-api.decentraland.zone"
                    : "https://auth-api.decentraland.org";

                string signatureUrl = Debug.isDebugBuild
                    ? "https://decentraland.zone/auth/requests"
                    : "https://decentraland.org/auth/requests";
#else
                const string serverUrl = "https://auth-api.decentraland.org";
                const string signatureUrl = "https://decentraland.org/auth/requests";
#endif

                var origin = new DappWeb3Authenticator(
                    new UnityAppWebBrowser(),
                    serverUrl,
                    signatureUrl,
                    identityCache,
                    new HashSet<string>(
                        new[]
                        {
                            "eth_getBalance",
                            "eth_call",
                            "eth_blockNumber",
                            "eth_signTypedData_v4",
                        }
                    )
                );

                originApi = origin;
                originAuth = origin;
            }

            public void Dispose()
            {
                originAuth.Dispose();// Disposes both
            }

            public UniTask<T> SendAsync<T>(EthApiRequest request, CancellationToken ct) =>
                originApi.SendAsync<T>(request, ct);

            public void AddVerificationListener(IVerifiedEthereumApi.VerificationDelegate callback) =>
                originApi.AddVerificationListener(callback);

            public UniTask<IWeb3Identity> LoginAsync(CancellationToken ct) =>
                originAuth.LoginAsync(ct);

            public UniTask LogoutAsync(CancellationToken cancellationToken) =>
                originAuth.LogoutAsync(cancellationToken);

            public void SetVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate? callback) =>
                originAuth.SetVerificationListener(callback);
        }
    }
}
