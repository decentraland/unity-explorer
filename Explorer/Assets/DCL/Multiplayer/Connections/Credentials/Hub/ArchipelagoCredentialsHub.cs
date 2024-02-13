using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.AdapterAddress;
using DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Typing;
using DCL.Web3.Identities;
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
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly string aboutUrl;
        private readonly TimeSpan heartbeatsInterval;

        public ArchipelagoCredentialsHub() : this(
            new WebRequestsAdapterAddresses(),
            new ArrayMemoryPool(ArrayPool<byte>.Shared!),
            new ThreadSafeMultiPool(),
            new IWeb3IdentityCache.Fake(),
            "https://realm-provider.decentraland.zone/main/about") { }

        public ArchipelagoCredentialsHub(IAdapterAddresses adapterAddresses, IMemoryPool memoryPool, IMultiPool multiPool, IWeb3IdentityCache web3IdentityCache, string aboutUrl)
        {
            this.aboutUrl = aboutUrl;
            this.web3IdentityCache = web3IdentityCache;
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

                //TODO sending heartbeats to the wss connection
                throw new NotImplementedException("IslandRoomCredentials is not implemented yet");
            }
            catch (Exception e) { throw new Exception("Cannot get island room credentials", e); }
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
            var account = web3IdentityCache
                         .EnsuredIdentity("Identity is not found")
                         .EphemeralAccount;

            string ethereumAddress = account.Address;
            using var challengeResponse = await connection.SendChallengeRequest(ethereumAddress, memoryPool, multiPool, token);
            string signedMessage = account.Sign(challengeResponse.value.ChallengeToSign!);
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
