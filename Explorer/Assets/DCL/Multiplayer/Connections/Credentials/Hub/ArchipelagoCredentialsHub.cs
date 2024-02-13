using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Typing;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Hub
{
    public class ArchipelagoCredentialsHub : ICredentialsHub
    {
        private readonly IAdapterAddresses adapterAddresses;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;
        private readonly string aboutUrl;
        private readonly TimeSpan heartbeatsInterval;

        public ArchipelagoCredentialsHub() : this(
            new WebRequestController(
                new WebRequestsAnalyticsContainer(),
                new PlayerPrefsIdentityProvider(
                    new PlayerPrefsIdentityProvider.DecentralandIdentityWithNethereumAccountJsonSerializer()
                )
            ),
            "https://realm-provider.decentraland.zone/main/about") { }

        //TODO inject dependencies
        public ArchipelagoCredentialsHub(IWebRequestController webRequestController, string aboutUrl)
        {
            this.aboutUrl = aboutUrl;
        }

        public UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async UniTask<ICredentials> IslandRoomCredentials(CancellationToken token)
        {
            string adapterUrl = await adapterAddresses.AdapterUrl(aboutUrl);
            var connection = await NewArchipelagoLiveConnection(adapterUrl, token);

            //about info
            //adapter url
            //open a connection against the adapter url via wss

            //TODO sending heartbeats to the wss connection
            throw new NotImplementedException();
        }

        private async UniTask<IArchipelagoLiveConnection> NewArchipelagoLiveConnection(string adapterUrl, CancellationToken token)
        {
            var connection = new HeartbeatsArchipelagoLiveConnection(
                new WebSocketArchipelagoLiveConnection(),
                memoryPool,
                heartbeatsInterval
            );

            await connection.ConnectAsync(adapterUrl, token);
            var result = await AuthorizedPeerId(connection, token);

            if (result.Success == false)
                throw new Exception(
                    "Cannot autorize with current ethereum address and signature, peer id is invalid"
                );

            connection.LaunchHeartbeats(token).Forget();
            return connection;
        }

        private async UniTask<LightResult<string>> AuthorizedPeerId(IArchipelagoLiveConnection connection, CancellationToken token)
        {
            string ethereumAddress = "TODO"; //TODO
            using var challengeResponse = await connection.SendChallengeRequest(ethereumAddress, memoryPool, multiPool, token);
            string signedMessage = "signed"; //TODO signed message
            var result = await connection.SendSignedChallenge(signedMessage, memoryPool, multiPool, token);

            if (result.Success)
            {
                using var welcome = result.Result;
                return new LightResult<string>(welcome.value.PeerId!, true);
            }

            return LightResult<string>.FAILURE;
        }
    }
}
