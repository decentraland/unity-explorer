using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementMessageBus : IDisposable
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private NetworkMessageEncoder messageEncoder = null!;

        private readonly World globalWorld;

        private bool isDisposed;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;

            this.messagePipesHub.IslandPipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);
        }

        public void InitializeEncoder(MessageEncodingSettings messageEncodingSettings)
        {
            messageEncoder = new NetworkMessageEncoder(messageEncodingSettings);
        }

        public void Dispose()
        {
            isDisposed = true;
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnMessageReceived(ReceivedMessage<MovementCompressed> receivedMessage)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed is bad");
                return;
            }

            using (receivedMessage)
            {
                if (cancellationTokenSource.Token.IsCancellationRequested)
                    return;

                CompressedNetworkMovementMessage message = new ()
                {
                    temporalData = receivedMessage.Payload.TemporalData,
                    movementData = receivedMessage.Payload.MovementData,
                };

                Inbox(messageEncoder.Decompress(message), receivedMessage.FromWalletId);
            }
        }

        public void Send(NetworkMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe());
            WriteAndSend(message, messagePipesHub.ScenePipe());
        }

        private void WriteAndSend(NetworkMovementMessage message, IMessagePipe messagePipe)
        {
            MessageWrap<MovementCompressed> messageWrap = messagePipe.NewMessage<MovementCompressed>();
            WriteToProto(messageEncoder.Compress(message), messageWrap.Payload);
            messageWrap.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
        }

        private static void WriteToProto(CompressedNetworkMovementMessage message, MovementCompressed proto)
        {
            proto.TemporalData = message.temporalData;
            proto.MovementData = message.movementData;
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            TryEnqueue(@for, fullMovementMessage);
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
        }

        private void TryEnqueue(string walletId, NetworkMovementMessage fullMovementMessage)
        {
            if (entityParticipantTable.Has(walletId) == false)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER_MOVEMENT, $"Entity for wallet {walletId} not found");
                return;
            }

            Entity entity = entityParticipantTable.Entity(walletId);

            if (globalWorld.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
                remotePlayerMovementComponent.Enqueue(fullMovementMessage);
        }

        /// <summary>
        ///     For Debug purposes only
        /// </summary>
        public async UniTaskVoid SelfSendWithDelayAsync(NetworkMovementMessage message, float delay)
        {
            CompressedNetworkMovementMessage compressedMessage = messageEncoder.Compress(message);
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);
            NetworkMovementMessage decompressedMessage = messageEncoder.Decompress(compressedMessage);

            Inbox(decompressedMessage, @for: RemotePlayerMovementComponent.TEST_ID);
        }
    }
}
