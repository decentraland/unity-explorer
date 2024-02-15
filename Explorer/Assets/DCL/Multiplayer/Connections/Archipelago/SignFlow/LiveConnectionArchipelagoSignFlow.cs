using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Credentials.Archipelago.LiveConnections;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Typing;
using Decentraland.Common;
using Decentraland.Kernel.Comms.V3;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System;
using System.Threading;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.Connections.Credentials.Archipelago.SignFlow
{
    public class LiveConnectionArchipelagoSignFlow : IArchipelagoSignFlow
    {
        private readonly IArchipelagoLiveConnection connection;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;

        public LiveConnectionArchipelagoSignFlow(IArchipelagoLiveConnection connection, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.connection = connection;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        public UniTask ConnectAsync(string adapterUrl, CancellationToken token) =>
            connection.ConnectAsync(adapterUrl, token);

        public async UniTask<string> MessageForSign(string ethereumAddress, CancellationToken token)
        {
            using var challenge = multiPool.TempResource<ChallengeRequestMessage>();
            challenge.value.Address = ethereumAddress;
            using var clientPacket = multiPool.TempResource<ClientPacket>();
            clientPacket.value.ClearMessage();
            clientPacket.value.ChallengeRequest = challenge.value;
            using var response = await connection.SendAndReceiveAsync(clientPacket.value, memoryPool, token);
            using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
            using var challengeResponse = new SmartWrap<ChallengeResponseMessage>(serverPacket.value.ChallengeResponse!, multiPool);
            return challengeResponse.value.ChallengeToSign!;
        }

        public async UniTask<LightResult<string>> WelcomePeerId(string signedMessageAuthChainJson, CancellationToken token)
        {
            using var signedMessage = multiPool.TempResource<SignedChallengeMessage>();
            signedMessage.value.AuthChainJson = signedMessageAuthChainJson;

            using var clientPacket = multiPool.TempResource<ClientPacket>();
            clientPacket.value.ClearMessage();
            clientPacket.value.SignedChallenge = signedMessage.value;

            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource().Token, token);

            var result = await UniTask.WhenAny(
                connection.SendAndReceiveAsync(clientPacket.value, memoryPool, linkedToken.Token),
                connection.WaitDisconnect(linkedToken.Token)
            );

            linkedToken.Cancel();

            if (result.hasResultLeft)
            {
                using var response = result.result;
                using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
                using var welcomeMessage = new SmartWrap<WelcomeMessage>(serverPacket.value.Welcome!, multiPool);
                return welcomeMessage.value.PeerId.AsSuccess();
            }

            return LightResult<string>.FAILURE;
        }

        public UniTask SendHeartbeat(Vector3 playerPosition, CancellationToken token)
        {
            using var position = multiPool.TempResource<Position>();
            position.value.X = playerPosition.x;
            position.value.Y = playerPosition.y;
            position.value.Z = playerPosition.z;

            using var heartbeat = multiPool.TempResource<Heartbeat>();
            heartbeat.value.Position = position.value;

            using var clientPacket = multiPool.TempResource<ClientPacket>();
            clientPacket.value.ClearMessage();
            clientPacket.value.Heartbeat = heartbeat.value;

            return connection.SendAsync(clientPacket.value, memoryPool, token);
        }

        public async UniTask StartListeningForConnectionString(Action<string> onNewConnectionString, CancellationToken token)
        {
            while (token.IsCancellationRequested == false)
            {
                var response = await connection.ReceiveAsync(token);
                using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);

                if (serverPacket.value.MessageCase is ServerPacket.MessageOneofCase.IslandChanged)
                {
                    using var islandChanged = new SmartWrap<IslandChangedMessage>(serverPacket.value.IslandChanged!, multiPool);
                    onNewConnectionString(islandChanged.value.ConnStr);
                }
            }
        }
    }
}
