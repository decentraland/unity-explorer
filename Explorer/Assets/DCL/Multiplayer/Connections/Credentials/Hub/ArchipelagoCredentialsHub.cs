using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Typing;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Buffers;
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
            new WebRequestsAdapterAddresses(),
            new ArrayMemoryPool(ArrayPool<byte>.Shared!),
            new ThreadSafeMultiPool(),
            "https://realm-provider.decentraland.zone/main/about"
        ) { }

        public ArchipelagoCredentialsHub(IAdapterAddresses adapterAddresses, IMemoryPool memoryPool, IMultiPool multiPool, string aboutUrl)
        {
            this.aboutUrl = aboutUrl;
            this.adapterAddresses = adapterAddresses;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        public UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async UniTask<ICredentials> IslandRoomCredentials(CancellationToken token)
        {
            try
            {
                string adapterUrl = await adapterAddresses.AdapterUrlAsync(aboutUrl, token);
                var connection = await NewArchipelagoLiveConnection(adapterUrl, token);

                //about info
                //adapter url
                //open a connection against the adapter url via wss

                //TODO sending heartbeats to the wss connection
                throw new NotImplementedException();
            }
            catch (Exception e)
            {
                throw new Exception("Cannot get island room credentials", e);
            }
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
            result.EnsureSuccess("Cannot authorize with current ethereum address and signature, peer id is invalid");
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
                return welcome.value.PeerId.AsSuccess();
            }

            return LightResult<string>.FAILURE;
        }
    }
}
