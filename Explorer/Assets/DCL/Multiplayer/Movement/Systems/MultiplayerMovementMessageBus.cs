using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;
using UnityEngine;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementMessageBus : IDisposable
    {
        private readonly IMessagePipesHub messagePipesHub;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private World globalWorld = null!;
        private bool isDisposed;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, IReadOnlyEntityParticipantTable entityParticipantTable)
        {
            this.messagePipesHub = messagePipesHub;
            this.entityParticipantTable = entityParticipantTable;

            this.messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnMessageReceived);
            this.messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnMessageReceived);
        }

        public void Dispose()
        {
            isDisposed = true;
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        private void OnMessageReceived(ReceivedMessage<Decentraland.Kernel.Comms.Rfc4.Movement> receivedMessage)
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

        public void Send(NetworkMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe());
            WriteAndSend(message, messagePipesHub.ScenePipe());
        }

        public void InjectWorld(World world)
        {
            globalWorld = world;
        }

        private void WriteAndSend(NetworkMovementMessage message, IMessagePipe messagePipe)
        {
            MessageWrap<Decentraland.Kernel.Comms.Rfc4.Movement> messageWrap = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
            WriteToProto(message, messageWrap.Payload);
            messageWrap.SendAndDisposeAsync(cancellationTokenSource.Token).Forget();
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

        private static NetworkMovementMessage MovementMessage(Decentraland.Kernel.Comms.Rfc4.Movement proto) =>
            new ()
            {
                timestamp = proto.Timestamp,
                position = new Vector3(proto.PositionX, proto.PositionY, proto.PositionZ),
                velocity = new Vector3(proto.VelocityX, proto.VelocityY, proto.VelocityZ),
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

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            Enqueue(@for, fullMovementMessage);
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
        }

        private void Enqueue(string walletId, NetworkMovementMessage fullMovementMessage)
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

        public async UniTaskVoid SelfSendWithDelayAsync(NetworkMovementMessage message, float delay)
        {
            CompressedNetworkMovementMessage compressedMessage = message.Compress();
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);
            NetworkMovementMessage decompressedMessage = compressedMessage.Decompress();

            decompressedMessage.enqueueTime = UnityEngine.Time.unscaledTime;

            // Debug.Log($"VVV {message.animState.ToString()}");
            // Debug.Log($"VVV {message.timestamp} - {decompressedMessage.timestamp} - {compressedMessage.compressedData}");
            Inbox(decompressedMessage, @for: RemotePlayerMovementComponent.TEST_ID);
        }
    }
}
