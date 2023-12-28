using Cysharp.Threading.Tasks;
using DCL.Browser;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Globalization;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class DappWeb3Authenticator : IWeb3VerifiedAuthenticator
    {
        private const string SERVER_URL_DEV = "https://auth-api.decentraland.zone";
        private const string SERVER_URL_PRD = "https://auth-api.decentraland.today";
        private const string SIGN_DAPP_URL_DEV = "https://decentraland.zone/auth/requests/:requestId";
        private const string SIGN_DAPP_URL_PRD = "https://decentraland.org/auth/requests/:requestId";
        private const int TIMEOUT_SECONDS = 30;

        private readonly IWebBrowser webBrowser;

        private SocketIO? webSocket;
        private UniTaskCompletionSource<DappSignatureResponse>? signatureOutcomeTask;
        private Action<int>? verificationCallback;

        public IWeb3Identity? Identity { get; private set; }

        public DappWeb3Authenticator(IWebBrowser webBrowser)
        {
            this.webBrowser = webBrowser;
        }

        public void Dispose()
        {
            webSocket?.Dispose();
            Identity?.Dispose();
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
                DateTime ephemeralMessageExpiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, ephemeralMessageExpiration);

                SignatureIdResponse authenticationResponse = await RequestAuthenticationAsync(ephemeralMessage, cancellationToken);

                await UniTask.SwitchToMainThread(cancellationToken);

                verificationCallback?.Invoke(authenticationResponse.code);

                DappSignatureResponse signature = await WaitForUserSignature(authenticationResponse.requestId, cancellationToken);

                AuthChain authChain = CreateAuthChain(signature, ephemeralMessage);

                var expiration = DateTime.Parse(authenticationResponse.expiration, null,
                    DateTimeStyles.RoundtripKind);
                // To keep cohesiveness between the platform, convert the user address to lower case
                Identity = new DecentralandIdentity(new Web3Address(signature.sender.ToLower()),
                    ephemeralAccount, expiration, authChain);

                return Identity;
            }
            finally
            {
                await TerminateWebSocket();
                await UniTask.SwitchToMainThread(cancellationToken);
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            Identity?.Dispose();

            await TerminateWebSocket();
        }

        public void AddVerificationListener(Action<int> callback) =>
            verificationCallback = callback;

        private SocketIO InitializeWebSocket()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var uri = new Uri(SERVER_URL_DEV);
#else
            var uri = new Uri(SERVER_URL_PRD);
#endif

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer();
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

            await webSocket!.EmitAsync("request",
                cancellationToken,
                r => task.TrySetResult(r.GetValue<SignatureIdResponse>()),
                new SignatureRequest
                {
                    method = "dcl_personal_sign",
                    @params = new[] { ephemeralMessage },
                });

            return await task.Task.Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
                             .AttachExternalCancellation(cancellationToken);
        }

        private async UniTask<DappSignatureResponse> WaitForUserSignature(string requestId, CancellationToken ct)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            webBrowser.OpenUrl(SIGN_DAPP_URL_DEV.Replace(":requestId", requestId));
#else
            webBrowser.OpenUrl(SIGN_DAPP_URL_PRD.Replace(":requestId", requestId));
#endif

            signatureOutcomeTask = new UniTaskCompletionSource<DappSignatureResponse>();

            return await signatureOutcomeTask.Task.AttachExternalCancellation(ct);
        }

        [Serializable]
        private struct DappSignatureResponse
        {
            public string requestId;
            public string result;
            public string sender;
        }

        [Serializable]
        private struct SignatureRequest
        {
            public string method;
            public string[] @params;
        }

        [Serializable]
        private struct SignatureIdResponse
        {
            public string requestId;
            public string expiration;
            public int code;
        }
    }
}
