using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Web3Authentication.Accounts;
using DCL.Web3Authentication.Chains;
using DCL.Web3Authentication.Identities;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace DCL.Web3Authentication.Signatures
{
    public partial class DappWeb3Authenticator : IWeb3VerifiedAuthenticator, IWeb3VerifiedSigner
    {
        private const int TIMEOUT_SECONDS = 30;

        private readonly IWebBrowser webBrowser;
        private readonly string serverUrl;
        private readonly string signatureUrl;
        private readonly IWeb3IdentityCache identityCache;

        private SocketIO? webSocket;
        private UniTaskCompletionSource<DappSignatureResponse>? signatureOutcomeTask;
        private IWeb3VerifiedAuthenticator.VerificationDelegate? loginVerificationCallback;
        private IWeb3VerifiedSigner.VerificationDelegate? signatureVerificationCallback;

        public DappWeb3Authenticator(IWebBrowser webBrowser,
            string serverUrl,
            string signatureUrl,
            IWeb3IdentityCache identityCache)
        {
            this.webBrowser = webBrowser;
            this.serverUrl = serverUrl;
            this.signatureUrl = signatureUrl;
            this.identityCache = identityCache;
        }

        public void Dispose()
        {
            try { webSocket?.Dispose(); }
            catch (ObjectDisposedException) { }
        }

        public async UniTask<Web3PersonalSignature> SignAsync(string payload, CancellationToken ct)
        {
            try
            {
                InitializeWebSocket();

                await ConnectToServerAsync();

                string challenge = identityCache.Identity!.EphemeralAccount.Sign(payload);

                SignatureIdResponse authenticationResponse = await RequestAuthorizedEthMethod(new AuthorizedEthApiRequest
                {
                    method = "personal_sign",
                    @params = new[]
                    {
                        challenge,
                        identityCache.Identity!.EphemeralAccount.Address,
                    },
                    authChain = identityCache.Identity!.AuthChain.ToArray(),
                }, ct);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(ct);

                signatureVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration);

                DappSignatureResponse signature = await RequestWalletConfirmationAsync(authenticationResponse.requestId,
                    signatureExpiration, ct);

                return new Web3PersonalSignature(signature.result, new Web3Address(signature.sender));
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
        /// <exception cref="Web3SignatureException"></exception>
        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken ct)
        {
            try
            {
                InitializeWebSocket();

                await ConnectToServerAsync();

                var ephemeralAccount = NethereumAccount.CreateRandom();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

                SignatureIdResponse authenticationResponse = await RequestEthMethod(
                    new EthApiRequest
                    {
                        method = "dcl_personal_sign",
                        @params = new[] { ephemeralMessage },
                    }, ct);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(ct);

                loginVerificationCallback?.Invoke(authenticationResponse.code, signatureExpiration);

                DappSignatureResponse signature = await RequestWalletConfirmationAsync(authenticationResponse.requestId,
                    signatureExpiration, ct);

                AuthChain authChain = CreateAuthChain(signature, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                return new DecentralandIdentity(new Web3Address(signature.sender.ToLower()),
                    ephemeralAccount, sessionExpiration, authChain);
            }
            finally
            {
                await DisconnectFromServerAsync();
                await UniTask.SwitchToMainThread(ct);
                await SignAsync("bleh", ct);
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await DisconnectFromServerAsync();
        }

        public void AddVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate callback) =>
            loginVerificationCallback = callback;

        public void AddVerificationListener(IWeb3VerifiedSigner.VerificationDelegate callback) =>
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

        private AuthChain CreateAuthChain(DappSignatureResponse signature, string ephemeralMessage)
        {
            var authChain = AuthChain.Create();

            authChain.Set(AuthLinkType.SIGNER, new AuthLink
            {
                type = AuthLinkType.SIGNER,
                payload = signature.sender,
                signature = "",
            });

            AuthLinkType ecdsaType = signature.result.Length == 132
                ? AuthLinkType.ECDSA_EPHEMERAL
                : AuthLinkType.ECDSA_EIP_1654_EPHEMERAL;

            authChain.Set(ecdsaType, new AuthLink
            {
                type = ecdsaType,
                payload = ephemeralMessage,
                signature = signature.result,
            });

            return authChain;
        }

        private string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";

        private async UniTask ConnectToServerAsync() =>
            await webSocket!.ConnectAsync().AsUniTask().Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS));

        private void ProcessSignatureOutcomeMessage(SocketIOResponse response)
        {
            signatureOutcomeTask?.TrySetResult(response.GetValue<DappSignatureResponse>());
        }

        private async UniTask<SignatureIdResponse> RequestAuthorizedEthMethod(
            AuthorizedEthApiRequest request,
            CancellationToken ct) =>
            await RequestEthMethod(request, ct);

        private async UniTask<SignatureIdResponse> RequestEthMethod(
            EthApiRequest request,
            CancellationToken ct)
        {
            UniTaskCompletionSource<SignatureIdResponse> task = new ();

            await webSocket!.EmitAsync("request", ct,
                r =>
                {
                    SignatureIdResponse signatureIdResponse = r.GetValue<SignatureIdResponse>();

                    if (!string.IsNullOrEmpty(signatureIdResponse.error))
                        task.TrySetException(new Web3SignatureException(signatureIdResponse.error));
                    else if (string.IsNullOrEmpty(signatureIdResponse.requestId))
                        task.TrySetException(new Web3SignatureException("Cannot solve auth request id"));
                    else
                        task.TrySetResult(signatureIdResponse);
                }, request);

            return await task.Task.Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
                             .AttachExternalCancellation(ct);
        }

        private async UniTask<DappSignatureResponse> RequestWalletConfirmationAsync(string requestId, DateTime expiration, CancellationToken ct)
        {
            webBrowser.OpenUrl($"{signatureUrl}/{requestId}");

            signatureOutcomeTask?.TrySetCanceled(ct);
            signatureOutcomeTask = new UniTaskCompletionSource<DappSignatureResponse>();

            TimeSpan duration = expiration - DateTime.UtcNow;

            try
            {
                DappSignatureResponse signature = await signatureOutcomeTask.Task.Timeout(duration).AttachExternalCancellation(ct);

                if (string.IsNullOrEmpty(signature.sender))
                    throw new Web3SignatureException($"Cannot solve the signer's address from the signature. Request id: {requestId}");

                if (string.IsNullOrEmpty(signature.result))
                    throw new Web3SignatureException($"Cannot solve the signature. Request id: {requestId}");

                return signature;
            }
            catch (TimeoutException) { throw new SignatureExpiredException(expiration); }
        }
    }
}
