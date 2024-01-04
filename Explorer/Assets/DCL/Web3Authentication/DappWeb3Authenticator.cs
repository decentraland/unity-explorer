using Cysharp.Threading.Tasks;
using DCL.Browser;
using Newtonsoft.Json;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Globalization;
using System.Threading;

namespace DCL.Web3Authentication
{
    public partial class DappWeb3Authenticator : IWeb3VerifiedAuthenticator
    {
        private const int TIMEOUT_SECONDS = 30;

        private readonly IWebBrowser webBrowser;
        private readonly string serverUrl;
        private readonly string signatureUrl;

        private SocketIO? webSocket;
        private UniTaskCompletionSource<DappSignatureResponse>? signatureOutcomeTask;
        private IWeb3VerifiedAuthenticator.VerificationDelegate? verificationCallback;

        public DappWeb3Authenticator(IWebBrowser webBrowser,
            string serverUrl,
            string signatureUrl)
        {
            this.webBrowser = webBrowser;
            this.serverUrl = serverUrl;
            this.signatureUrl = signatureUrl;
        }

        public void Dispose()
        {
            webSocket?.Dispose();
        }

        /// <summary>
        ///     1. An authentication request is sent to the server
        ///     2. Open a tab to let the user sign through the browser with his custom installed wallet
        ///     3. Use the signature information to generate the identity
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="Web3SignatureException"></exception>
        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken)
        {
            try
            {
                InitializeWebSocket();

                await ConnectToServerAsync();

                var ephemeralAccount = NethereumAccount.CreateRandom();

                // 1 week expiration day, just like unity-renderer
                DateTime sessionExpiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, sessionExpiration);

                SignatureIdResponse authenticationResponse = await RequestAuthenticationAsync(ephemeralMessage, cancellationToken);

                DateTime signatureExpiration = DateTime.UtcNow.AddMinutes(5);

                if (!string.IsNullOrEmpty(authenticationResponse.expiration))
                    signatureExpiration = DateTime.Parse(authenticationResponse.expiration, null, DateTimeStyles.RoundtripKind);

                await UniTask.SwitchToMainThread(cancellationToken);

                verificationCallback?.Invoke(authenticationResponse.code, signatureExpiration);

                DappSignatureResponse signature = await WaitForUserSignatureAsync(authenticationResponse.requestId,
                    signatureExpiration, cancellationToken);

                AuthChain authChain = CreateAuthChain(signature, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                return new DecentralandIdentity(new Web3Address(signature.sender.ToLower()),
                    ephemeralAccount, sessionExpiration, authChain);
            }
            finally
            {
                await TerminateWebSocket();
                await UniTask.SwitchToMainThread(cancellationToken);
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            await TerminateWebSocket();
        }

        public void AddVerificationListener(IWeb3VerifiedAuthenticator.VerificationDelegate callback) =>
            verificationCallback = callback;

        private SocketIO InitializeWebSocket()
        {
            var uri = new Uri(serverUrl);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer(new JsonSerializerSettings());

            webSocket.On("outcome", ProcessSignatureOutcomeMessage);

            return webSocket;
        }

        private async UniTask TerminateWebSocket()
        {
            if (webSocket != null)
            {
                if (webSocket.Connected)
                    await webSocket.DisconnectAsync();

                try { webSocket.Dispose(); }
                catch (ObjectDisposedException) { }

                webSocket = null;
            }
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

        private async UniTask<SignatureIdResponse> RequestAuthenticationAsync(string ephemeralMessage,
            CancellationToken cancellationToken)
        {
            UniTaskCompletionSource<SignatureIdResponse> task = new ();

            await webSocket!.EmitAsync("request", cancellationToken,
                r =>
                {
                    SignatureIdResponse signatureIdResponse = r.GetValue<SignatureIdResponse>();

                    if (string.IsNullOrEmpty(signatureIdResponse.requestId))
                        task.TrySetException(new Web3SignatureException("Cannot solve auth request id"));
                    else
                        task.TrySetResult(signatureIdResponse);
                },
                new SignatureRequest
                {
                    method = "dcl_personal_sign",
                    @params = new[] { ephemeralMessage },
                });

            return await task.Task.Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
                             .AttachExternalCancellation(cancellationToken);
        }

        private async UniTask<DappSignatureResponse> WaitForUserSignatureAsync(string requestId, DateTime expiration, CancellationToken ct)
        {
            webBrowser.OpenUrl($"{signatureUrl}/{requestId}");

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
