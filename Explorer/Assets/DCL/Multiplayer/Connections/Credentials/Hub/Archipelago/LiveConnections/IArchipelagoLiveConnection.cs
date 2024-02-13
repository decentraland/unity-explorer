using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Pools;
using DCL.Multiplayer.Connections.Typing;
using Decentraland.Kernel.Comms.V3;
using Google.Protobuf;
using LiveKit.client_sdk_unity.Runtime.Scripts.Internal.FFIClients;
using LiveKit.Internal.FFIClients.Pools;
using LiveKit.Internal.FFIClients.Pools.Memory;
using System.Threading;
using WelcomeMessage = Decentraland.Kernel.Comms.V3.WelcomeMessage;

namespace DCL.Multiplayer.Connections.Credentials.Hub.Archipelago.LiveConnections
{
    public interface IArchipelagoLiveConnection
    {
        bool Connected();

        UniTask ConnectAsync(string adapterUrl, CancellationToken token);

        UniTask DisconnectAsync(CancellationToken token);

        /// <param name="data">takes the ownership for the data</param>
        /// <param name="token">cancellation token</param>
        /// <returns>returns a memory chunk ang gives the ownership for it</returns>
        UniTask SendAsync(MemoryWrap data, CancellationToken token);

        UniTask<MemoryWrap> ReceiveAsync(CancellationToken token);
    }

    public static class ArchipelagoLiveConnectionExtensions
    {
        /// <summary>
        /// Takes ownership for the data and returns the ownership for the result
        /// </summary>
        public static async UniTask<MemoryWrap> SendAndReceiveAsync(this IArchipelagoLiveConnection archipelagoLiveConnection, MemoryWrap data, CancellationToken token)
        {
            await archipelagoLiveConnection.SendAsync(data, token);
            return await archipelagoLiveConnection.ReceiveAsync(token);
        }

        public static async UniTask<MemoryWrap> SendAndReceiveAsync<T>(this IArchipelagoLiveConnection connection, T message, IMemoryPool memoryPool, CancellationToken token) where T: IMessage
        {
            using var memory = memoryPool.Memory(message);
            Write();
            var result = await connection.SendAndReceiveAsync(memory, token);
            return result;

            void Write()
            {
                var span = memory.Span();
                message.WriteTo(span);
            }
        }

        public static async UniTask<SmartWrap<ChallengeResponseMessage>> SendChallengeRequest(
            this IArchipelagoLiveConnection connection,
            string ethereumAddress,
            IMemoryPool memoryPool,
            IMultiPool multiPool,
            CancellationToken token
        )
        {
            using var challenge = multiPool.TempResource<ChallengeRequestMessage>();
            challenge.value.Address = ethereumAddress;
            using var response = await connection.SendAndReceiveAsync(challenge.value, memoryPool, token);
            var serverPacket = ServerPacket.Parser.ParseFrom(response.Span());
            return new SmartWrap<ChallengeResponseMessage>(serverPacket.ChallengeResponse!, multiPool);
        }

        public static async UniTask<LightResult<SmartWrap<WelcomeMessage>>> SendSignedChallenge(
            this IArchipelagoLiveConnection connection,
            string challenge,
            IMemoryPool memoryPool,
            IMultiPool multiPool,
            CancellationToken token
        )
        {
            using var signedMessage = multiPool.TempResource<SignedChallengeMessage>();
            signedMessage.value.AuthChainJson = challenge;


            var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(new CancellationTokenSource().Token, token);

            var result = await UniTask.WhenAny(
                connection.SendAndReceiveAsync(signedMessage.value, memoryPool, linkedToken.Token),
                connection.WaitDisconnect(linkedToken.Token)
            );

            linkedToken.Cancel();

            if (result.hasResultLeft)
            {
                using var response = result.result;
                var serverPacket = ServerPacket.Parser.ParseFrom(response.Span());

                return new SmartWrap<WelcomeMessage>(serverPacket.Welcome!, multiPool).AsSuccess();
            }

            return LightResult<SmartWrap<WelcomeMessage>>.FAILURE;
        }

        public static async UniTask WaitDisconnect(this IArchipelagoLiveConnection connection, CancellationToken token)
        {
            while (token.IsCancellationRequested == false && connection.Connected())
                await UniTask.Yield();
        }
    }
}
