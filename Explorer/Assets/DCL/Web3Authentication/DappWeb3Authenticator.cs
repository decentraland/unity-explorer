using Cysharp.Threading.Tasks;
using SocketIOClient;
using SocketIOClient.Newtonsoft.Json;
using SocketIOClient.Transport;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.Web3Authentication
{
    public class DappWeb3Authenticator : IWeb3Authenticator
    {
        // TODO: switch urls depending on current realm / developer mode (?)
        private const string SERVER_URL_DEV = "https://auth-api.decentraland.zone";
        private const string SERVER_URL_PRD = "https://auth-api.decentraland.today";
        private const string SIGN_DAPP_URL_DEV = "https://decentraland.zone/auth/requests/:requestId";
        private const string SIGN_DAPP_URL_PRD = "https://decentraland.org/auth/requests/:requestId";

        private readonly Dictionary<string, UniTaskCompletionSource<SocketIOResponse>> pendingMessageTasks = new ();

        private SocketIO? webSocket;

        public IWeb3Identity? Identity { get; private set; }

        public void Dispose()
        {
            webSocket?.Dispose();
            Identity = null;
        }

        public async UniTask<IWeb3Identity> LoginAsync(CancellationToken cancellationToken)
        {
            var uri = new Uri(SERVER_URL_DEV);

            webSocket = new SocketIO(uri, new SocketIOOptions
            {
                Transport = TransportProtocol.WebSocket,
            });

            webSocket.JsonSerializer = new NewtonsoftJsonSerializer();

            webSocket.OnAny(ProcessMessage);

            await webSocket.ConnectAsync();

            var ephemeralAccount = NethereumAccount.CreateRandom();
            DateTime expiration = DateTime.UtcNow.AddMinutes(600);
            var ephemeralMessage = $"Decentraland Login\nEphemeral address: {ephemeralAccount.Address}\nExpiration: {expiration:s}";
            SignatureIdResponse authenticationResponse = await RequestAuthentication(ephemeralMessage, cancellationToken);

            await UniTask.SwitchToMainThread(cancellationToken);

            OpenDapp(authenticationResponse.payload.requestId);

            SignatureFromDappResponse signature = await WaitForMessageResponse<SignatureFromDappResponse>("submit-signature-response", cancellationToken);

            if (!signature.payload.ok)
                throw new Web3AuthenticationException("The user rejected the signature");

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

            Identity = new DecentralandIdentity(signature.payload.signer, ephemeralAccount, expiration, authChain);

            await webSocket.DisconnectAsync();
            webSocket.Dispose();
            webSocket = null;

            await UniTask.SwitchToMainThread(cancellationToken);

            return Identity;
        }

        public async UniTask LogoutAsync(CancellationToken cancellationToken)
        {
            Identity = null;

            if (webSocket == null) return;

            await webSocket.DisconnectAsync();
            webSocket.Dispose();
        }

        private static void OpenDapp(string requestId) =>

            // TODO: missing dependency inversion for opening browser tab
            Application.OpenURL(SIGN_DAPP_URL_DEV.Replace(":requestId", requestId));

        private void ProcessMessage(string name, SocketIOResponse response)
        {
            if (name != "message") return;

            MessageResponse message = response.GetValue<MessageResponse>();

            if (!pendingMessageTasks.TryGetValue(message.type, out UniTaskCompletionSource<SocketIOResponse>? task)) return;

            task.TrySetResult(response);
            pendingMessageTasks.Remove(message.type);
        }

        private async UniTask<SignatureIdResponse> RequestAuthentication(string payload,
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
                        data = payload,
                    },
                });

            return await WaitForMessageResponse<SignatureIdResponse>("request-response", cancellationToken);
        }

        private async UniTask<T> WaitForMessageResponse<T>(string eventName, CancellationToken cancellationToken)
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
        private struct SignatureFromDappResponse
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
