using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementMessageBus : IDisposable
    {
        public enum Scheme
        {
            Uncompressed,
            Compressed,
        }

        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private NetworkMessageEncoder messageEncoder;

        private readonly World globalWorld;
        private readonly Scheme scheme;

        private bool isDisposed;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable, World globalWorld, Scheme scheme)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
            this.scheme = scheme;

            this.messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);

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

        private void OnOldSchemaMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Movement> receivedMessage)
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

                NetworkMovementMessage message = MovementMessage(receivedMessage.Payload);
                Inbox(message, receivedMessage.FromWalletId);
            }
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

        private static NetworkMovementMessage MovementMessage(Decentraland.Kernel.Comms.Rfc4.Movement proto)
        {
            var vel = new Vector3(proto.VelocityX, proto.VelocityY, proto.VelocityZ);

            return new NetworkMovementMessage
            {
                timestamp = proto.Timestamp,
                position = new Vector3(proto.PositionX, proto.PositionY, proto.PositionZ),
                velocity = vel,
                velocitySqrMagnitude = vel.sqrMagnitude,

                animState = new AnimationStates
                {
                    MovementBlendValue = proto.MovementBlendValue,
                    SlideBlendValue = proto.SlideBlendValue,
                    IsGrounded = proto.IsGrounded,
                    IsJumping = proto.IsJumping,
                    IsLongJump = proto.IsLongJump,
                    IsFalling = proto.IsFalling,
                    IsLongFall = proto.IsLongFall,
                },
                isStunned = proto.IsStunned,
            };
        }

        private void WriteAndSend(NetworkMovementMessage message, IMessagePipe messagePipe)
        {
            switch (scheme)
            {
                case Scheme.Uncompressed:
                {
                    var messageWrap = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
                    WriteToProto(message, messageWrap.Payload);
                    messageWrap.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
                }

                    break;
                case Scheme.Compressed:
                {
                    MessageWrap<MovementCompressed> messageWrap = messagePipe.NewMessage<MovementCompressed>();
                    WriteToProto(messageEncoder.Compress(message), messageWrap.Payload);
                    messageWrap.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
                }

                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static void WriteToProto(NetworkMovementMessage message, Decentraland.Kernel.Comms.Rfc4.Movement movement)
        {
            movement.Timestamp = message.timestamp;

            movement.PositionX = message.position.x;
            movement.PositionY = message.position.y;
            movement.PositionZ = message.position.z;

            movement.VelocityX = message.velocity.x;
            movement.VelocityY = message.velocity.y;
            movement.VelocityZ = message.velocity.z;

            movement.MovementBlendValue = message.animState.MovementBlendValue;
            movement.SlideBlendValue = message.animState.SlideBlendValue;

            movement.IsGrounded = message.animState.IsGrounded;
            movement.IsJumping = message.animState.IsJumping;
            movement.IsLongJump = message.animState.IsLongJump;
            movement.IsFalling = message.animState.IsFalling;
            movement.IsLongFall = message.animState.IsLongFall;

            movement.IsStunned = message.isStunned;
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
