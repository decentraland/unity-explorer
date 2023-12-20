using Cysharp.Threading.Tasks;
using DCL.Browser;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.Web3Authentication
{
    public class DappWeb3Authenticator : IWeb3Authenticator
    {
        private const string SERVER_URL_DEV = "https://auth-api.decentraland.zone";
        private const string SERVER_URL_PRD = "https://auth-api.decentraland.today";
        private const string SIGN_DAPP_URL_DEV = "https://decentraland.zone/auth/requests/:requestId";
        private const string SIGN_DAPP_URL_PRD = "https://decentraland.org/auth/requests/:requestId";

        private readonly IWebBrowser webBrowser;
        private readonly Dictionary<string, UniTaskCompletionSource<SocketIOResponse>> pendingMessageTasks = new ();

        private SocketIO? webSocket;

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
                DateTime expiration = DateTime.UtcNow.AddDays(7);
                string ephemeralMessage = CreateEphemeralMessage(ephemeralAccount, expiration);

                SignatureIdResponse authenticationResponse = await RequestAuthenticationAsync(ephemeralMessage, cancellationToken);

                await UniTask.SwitchToMainThread(cancellationToken);

                LetUserSignThroughDapp(authenticationResponse.payload.requestId);
                DappSignatureResponse signature = await WaitForMessageResponseAsync<DappSignatureResponse>("submit-signature-response", cancellationToken);

                if (!signature.payload.ok)
                    throw new Web3SignatureException($"Signature failed: {authenticationResponse.payload.requestId}");

                AuthChain authChain = CreateAuthChain(signature, ephemeralMessage);

                // To keep cohesiveness between the platform, convert the user address to lower case
                Identity = new DecentralandIdentity(new Web3Address(signature.payload.signer.ToLower()),
                    ephemeralAccount, expiration, authChain);

                return Identity;
            }
            finally
            {
                if (webSocket != null)
                {
                    if (webSocket.Connected)
                        await webSocket.DisconnectAsync();

                    webSocket.Dispose();
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
            webSocket.OnAny(ProcessMessage);

            return webSocket;
        }

        private AuthChain CreateAuthChain(DappSignatureResponse signature, string ephemeralMessage)
        {
            var authChain = AuthChain.Create();

            authChain.Set(AuthLinkType.SIGNER, new AuthLink
            {
                type = AuthLinkType.SIGNER,
                payload = signature.payload.signer,
                signature = "",
            });

            authChain.Set(AuthLinkType.ECDSA_EPHEMERAL, new AuthLink
            {
                type = AuthLinkType.ECDSA_EPHEMERAL,
                payload = ephemeralMessage,
                signature = signature.payload.signature,
            });

            return authChain;
        }

        private string CreateEphemeralMessage(IWeb3Account ephemeralAccount, DateTime expiration) =>
            $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";

        private async UniTask ConnectToServerAsync() =>
            await webSocket!.ConnectAsync();

        private void LetUserSignThroughDapp(string requestId) =>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            webBrowser.OpenUrl(SIGN_DAPP_URL_DEV.Replace(":requestId", requestId));
#else
            webBrowser.OpenUrl(SIGN_DAPP_URL_PRD.Replace(":requestId", requestId));
#endif

        private void ProcessMessage(string name, SocketIOResponse response)
        {
            if (name != "message") return;

            MessageResponse message = response.GetValue<MessageResponse>();

            if (!pendingMessageTasks.TryGetValue(message.type, out UniTaskCompletionSource<SocketIOResponse>? task)) return;

            task.TrySetResult(response);
            pendingMessageTasks.Remove(message.type);
        }

        private async UniTask<SignatureIdResponse> RequestAuthenticationAsync(string ephemeralMessage,
            CancellationToken cancellationToken)
        {
            await webSocket!.EmitAsync("message",
                cancellationToken,
                new SignatureRequest
                {
                    type = "request",
                    payload = new SignatureRequest.Payload
                    {
                        type = "signature",
                        data = ephemeralMessage,
                    },
                });

            return await WaitForMessageResponseAsync<SignatureIdResponse>("request-response", cancellationToken);
        }

        private async UniTask<T> WaitForMessageResponseAsync<T>(string eventName, CancellationToken cancellationToken)
        {
            if (pendingMessageTasks.TryGetValue(eventName, out UniTaskCompletionSource<SocketIOResponse>? task))
                return (await task.Task.AttachExternalCancellation(cancellationToken))
                   .GetValue<T>();

            task = new UniTaskCompletionSource<SocketIOResponse>();
            pendingMessageTasks[eventName] = task;

            return (await task.Task.AttachExternalCancellation(cancellationToken))
               .GetValue<T>();
        }

        [Serializable]
        private struct DappSignatureResponse
        {
            public string type;
            public Payload payload;

            [Serializable]
            public struct Payload
            {
                public bool ok;
                public string requestId;
                public string signature;
                public string signer;
            }
        }

        [Serializable]
        private struct SignatureRequest
        {
            public string type;
            public Payload payload;

            [Serializable]
            public struct Payload
            {
                public string type;
                public string data;
            }
        }

        [Serializable]
        private struct SignatureIdResponse
        {
            public string type;
            public Payload payload;

            [Serializable]
            public struct Payload
            {
                public string requestId;
            }
        }

        [Serializable]
        private struct MessageResponse
        {
            public string type;
        }
    }
}
