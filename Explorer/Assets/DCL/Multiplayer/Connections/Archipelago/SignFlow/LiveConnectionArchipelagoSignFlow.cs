using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Archipelago.LiveConnections;
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
using Utility.Multithreading;
using Utility.Types;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.Connections.Archipelago.SignFlow
{
    /// <summary>
    ///     Runs heavy operations in a thread pool and uses a live connection to communicate with the server.
    /// </summary>
    public class LiveConnectionArchipelagoSignFlow : IArchipelagoSignFlow
    {
        private readonly AutoReconnectLiveConnection connection;
        private readonly IMemoryPool memoryPool;
        private readonly IMultiPool multiPool;

        /// <param name="connection">Relies on capabilities of auto-reconnection to transport</param>
        public LiveConnectionArchipelagoSignFlow(AutoReconnectLiveConnection connection, IMemoryPool memoryPool, IMultiPool multiPool)
        {
            this.connection = connection;
            this.memoryPool = memoryPool;
            this.multiPool = multiPool;
        }

        public async UniTask<Result> ReconnectAsync(string adapterUrl, CancellationToken token)
        {
            Result result;

            if (connection.IsConnected)
            {
                result = await connection.DisconnectAsync(token);

                if (!result.Success)
                    return result;
            }

            result = await connection.ConnectAsync(adapterUrl, token);
            return result;
        }

        public async UniTask<LightResult<string>> MessageForSignAsync(string ethereumAddress, CancellationToken token)
        {
            using SmartWrap<ChallengeRequestMessage> challenge = multiPool.TempResource<ChallengeRequestMessage>();
            challenge.value.Address = ethereumAddress;
            using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
            clientPacket.value.ClearMessage();
            clientPacket.value.ChallengeRequest = challenge.value;
            EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await connection.SendAndReceiveAsync(clientPacket.value, memoryPool, token);

            if (result.Success == false)
            {
                ReportHub.LogError(ReportCategory.LIVEKIT, $"Cannot message for sign for address {ethereumAddress}: {result.Error?.Message}");
                return LightResult<string>.FAILURE;
            }

            using MemoryWrap response = result.Value;
            using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
            using var challengeResponse = new SmartWrap<ChallengeResponseMessage>(serverPacket.value.ChallengeResponse!, multiPool);
            return challengeResponse.value.ChallengeToSign!.AsSuccess();
        }

        public async UniTask<LightResult<string>> WelcomePeerIdAsync(string signedMessageAuthChainJson, CancellationToken token)
        {
            try
            {
                using SmartWrap<SignedChallengeMessage> signedMessage = multiPool.TempResource<SignedChallengeMessage>();
                signedMessage.value.AuthChainJson = signedMessageAuthChainJson;

                using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
                clientPacket.value.ClearMessage();
                clientPacket.value.SignedChallenge = signedMessage.value;

                var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource().Token, token);

                (bool hasResultLeft, EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result) result = await UniTask.WhenAny(
                    connection.SendAndReceiveAsync(clientPacket.value, memoryPool, linkedToken.Token),
                    connection.WaitDisconnectAsync(linkedToken.Token)
                );

                linkedToken.Cancel();

                if (result.hasResultLeft)
                {
                    if (result.result.Success == false)
                        return LightResult<string>.FAILURE;

                    using MemoryWrap response = result.result.Value;
                    using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);
                    using var welcomeMessage = new SmartWrap<WelcomeMessage>(serverPacket.value.Welcome!, multiPool);
                    return welcomeMessage.value.PeerId.AsSuccess();
                }

                return LightResult<string>.FAILURE;
            }
            catch (Exception e) { ReportHub.LogException(new Exception($"Cannot welcome peer id for signed message {signedMessageAuthChainJson}", e), ReportCategory.LIVEKIT); }

            return LightResult<string>.FAILURE;
        }

        public async UniTask<Result> SendHeartbeatAsync(Vector3 playerPosition, CancellationToken token)
        {
            try
            {
                using SmartWrap<Position> position = multiPool.TempResource<Position>();
                position.value.X = playerPosition.x;
                position.value.Y = playerPosition.y;
                position.value.Z = playerPosition.z;

                using SmartWrap<Heartbeat> heartbeat = multiPool.TempResource<Heartbeat>();
                heartbeat.value.Position = position.value;

                using SmartWrap<ClientPacket> clientPacket = multiPool.TempResource<ClientPacket>();
                clientPacket.value.ClearMessage();
                clientPacket.value.Heartbeat = heartbeat.value;

                var result = await connection.SendAsync(clientPacket.value, memoryPool, token);

                return result.Success == false
                    ? Result.ErrorResult($"Cannot send heartbeat for position {playerPosition}: {result.Error!.Value.Message}")
                    : Result.SuccessResult();
            }
            // It seems to be not required
            catch (Exception e)
            {
                return Result.ErrorResult($"Cannot send heartbeat for position {playerPosition}: {e}");;
            }
        }

        /// <summary>
        ///     This loop is launched once and should be free from exceptions
        /// </summary>
        public async UniTaskVoid StartListeningForConnectionStringAsync(Action<string> onNewConnectionString, CancellationToken token)
        {
            await ExecuteOnThreadPoolScope.NewScopeAsync();

            while (token.IsCancellationRequested == false)
            {
                EnumResult<MemoryWrap, IArchipelagoLiveConnection.ResponseError> result = await connection.ReceiveAsync(token);

                if (result.Success == false)
                {
                    // AutoReconnectLiveConnection will recover the transport itself
                    if (token.IsCancellationRequested == false)
                        ReportHub.LogError(ReportCategory.LIVEKIT, $"Cannot listen for connection string: {result.Error?.Message}");

                    continue;
                }

                using MemoryWrap response = result.Value;
                using var serverPacket = new SmartWrap<ServerPacket>(response.AsMessageServerPacket(), multiPool);

                if (serverPacket.value.MessageCase is ServerPacket.MessageOneofCase.IslandChanged)
                {
                    using var islandChanged = new SmartWrap<IslandChangedMessage>(serverPacket.value.IslandChanged!, multiPool);
                    onNewConnectionString(islandChanged.value.ConnStr);
                }
            }
        }

        public UniTask DisconnectAsync(CancellationToken token) =>
            connection.DisconnectAsync(token);
    }
}
