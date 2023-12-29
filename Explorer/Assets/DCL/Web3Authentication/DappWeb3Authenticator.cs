using Cysharp.Threading.Tasks;
using DCL.Browser;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class DappWeb3Authenticator : IWeb3Authenticator, IWeb3IdentityProvider
    {
        private const int TIMEOUT_SECONDS = 30;

        private readonly IWebBrowser webBrowser;
        private readonly string serverUrl;
        private readonly string signatureUrl;

        private SocketIO? webSocket;
        private UniTaskCompletionSource<DappSignatureResponse>? signatureOutcomeTask;
        private UniTaskCompletionSource<IWeb3Identity>? identitySolvedTask;

        public IWeb3Identity? Identity { get; private set; }

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
            Identity?.Dispose();
        }

        public async UniTask<IWeb3Identity> GetOwnIdentityAsync(CancellationToken ct)
        {
            identitySolvedTask ??= new UniTaskCompletionSource<IWeb3Identity>();
            return await identitySolvedTask.Task.AttachExternalCancellation(ct);
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
                DateTime expiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, expiration);

                SignatureIdResponse authenticationResponse = await RequestAuthenticationAsync(ephemeralMessage, cancellationToken);

                await UniTask.SwitchToMainThread(cancellationToken);

                LetUserSignThroughDapp(authenticationResponse.requestId);
                DappSignatureResponse signature = await WaitForSignatureOutcome(cancellationToken);

                AuthChain authChain = CreateAuthChain(signature, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                Identity = new DecentralandIdentity(new Web3Address(signature.sender.ToLower()),
                    ephemeralAccount, expiration, authChain);

                identitySolvedTask?.TrySetResult(Identity);
                identitySolvedTask = null;

                return Identity;
            }
            finally
            {
                if (webSocket != null)
                {
                    if (webSocket.Connected)
                        await webSocket.DisconnectAsync();

                    try { webSocket.Dispose(); }
                    catch (ObjectDisposedException) { }

                    webSocket = null;
                }

                await UniTask.SwitchToMainThread(cancellationToken);
            }
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            Identity?.Dispose();

            if (webSocket == null) return;

            await webSocket.DisconnectAsync();
            webSocket.Dispose();
        }

        private SocketIO InitializeWebSocket()
        {
            var uri = new Uri(serverUrl);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer();
            webSocket.On("outcome", ProcessSignatureOutcomeMessage);

            return webSocket;
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

        private void LetUserSignThroughDapp(string requestId) =>
            webBrowser.OpenUrl($"{signatureUrl}/{requestId}");

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

        private async UniTask<DappSignatureResponse> WaitForSignatureOutcome(CancellationToken ct)
        {
            signatureOutcomeTask = new UniTaskCompletionSource<DappSignatureResponse>();

            return await signatureOutcomeTask.Task.Timeout(TimeSpan.FromSeconds(TIMEOUT_SECONDS))
                                             .AttachExternalCancellation(ct);
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
