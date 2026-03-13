using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Movement.Settings;
using Decentraland.Kernel.Comms.Rfc4;
using System;
using System.Threading;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace DCL.Multiplayer.Movement.Systems
{
    public class MultiplayerMovementMessageBus : IDisposable
    {
        public enum Scheme
        {
            Uncompressed,
            Compressed,
        }

        internal const float WALK_EPSILON = 0.05f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly MovementInbox movementInbox;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        private NetworkMessageEncoder? messageEncoder;
        private bool isDisposed;
        private IMultiplayerMovementSettings? settingsValue;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub, MovementInbox movementInbox)
        {
            this.messagePipesHub = messagePipesHub;
            this.movementInbox = movementInbox;

            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);
            messagePipesHub.IslandPipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);
        }

        public void Dispose()
        {
            isDisposed = true;
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void InitializeEncoder(MessageEncodingSettings messageEncodingSettings, IMultiplayerMovementSettings settingsValue, ParcelEncoder parcelEncoder)
        {
            this.settingsValue = settingsValue;
            messageEncoder = new NetworkMessageEncoder(messageEncodingSettings, parcelEncoder);
        }

        public void Send(NetworkMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe());
            WriteAndSend(message, messagePipesHub.ScenePipe());
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

                Inbox(UncompressedMovementMessage(receivedMessage.Payload), receivedMessage.FromWalletId);
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
                    headSyncData = receivedMessage.Payload.HeadSyncData,
                };

                Inbox(messageEncoder.Decompress(message), receivedMessage.FromWalletId);
            }
        }

        private static NetworkMovementMessage UncompressedMovementMessage(Decentraland.Kernel.Comms.Rfc4.Movement proto)
        {
            var vel = new Vector3(proto.VelocityX, proto.VelocityY, proto.VelocityZ);

            float movementBlend = Mathf.Clamp(proto.MovementBlendValue, 0, 3);
            var movementKind = (MovementKind)Mathf.Max(Mathf.RoundToInt(movementBlend), movementBlend > WALK_EPSILON ? 1 : 0);

            return new NetworkMovementMessage
            {
                timestamp = proto.Timestamp,
                position = new Vector3(proto.PositionX, proto.PositionY, proto.PositionZ),
                rotationY = proto.RotationY,

                velocity = vel,
                velocitySqrMagnitude = vel.sqrMagnitude,

                movementKind = movementKind,

                animState = new AnimationStates
                {
                    MovementBlendValue = movementBlend,
                    SlideBlendValue = proto.SlideBlendValue,
                    IsGrounded = proto.IsGrounded,
                    JumpCount = proto.JumpCount,
                    IsLongJump = proto.IsLongJump,
                    IsLongFall = proto.IsLongFall,
                    IsFalling = proto.IsFalling,
                    IsStunned = proto.IsStunned,
                    GlideState = (GlideStateValue)proto.GlideState,
                },
                isStunned = proto.IsStunned,
                isInstant = proto.IsInstant,
                isEmoting = proto.IsEmoting,

                headIKYawEnabled = proto.HeadIkYawEnabled,
                headIKPitchEnabled = proto.HeadIkPitchEnabled,
                headYawAndPitch = new Vector2(proto.HeadYaw, proto.HeadPitch),
            };
        }

        private void WriteAndSend(NetworkMovementMessage message, IMessagePipe messagePipe)
        {
            Scheme schema = settingsValue.UseCompression ? Scheme.Compressed : Scheme.Uncompressed;

            switch (schema)
            {
                case Scheme.Uncompressed:
                {
                    MessageWrap<Decentraland.Kernel.Comms.Rfc4.Movement> messageWrap = messagePipe.NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
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

            movement.RotationY = message.rotationY;

            movement.VelocityX = message.velocity.x;
            movement.VelocityY = message.velocity.y;
            movement.VelocityZ = message.velocity.z;

            movement.MovementBlendValue = message.animState.MovementBlendValue;
            movement.SlideBlendValue = message.animState.SlideBlendValue;

            movement.IsGrounded = message.animState.IsGrounded;
            movement.JumpCount = message.animState.JumpCount;
            movement.IsLongJump = message.animState.IsLongJump;
            movement.IsLongFall = message.animState.IsLongFall;
            movement.IsFalling = message.animState.IsFalling;
            movement.GlideState = (Decentraland.Kernel.Comms.Rfc4.Movement.Types.GlideState)message.animState.GlideState;
            movement.IsStunned = message.isStunned;
            movement.IsInstant = message.isInstant;
            movement.IsEmoting = message.isEmoting;

            movement.HeadIkYawEnabled = message.headIKYawEnabled;
            movement.HeadIkPitchEnabled = message.headIKPitchEnabled;
            movement.HeadYaw = message.headYawAndPitch.x;
            movement.HeadPitch = message.headYawAndPitch.y;
        }

        private static void WriteToProto(CompressedNetworkMovementMessage message, MovementCompressed proto)
        {
            proto.TemporalData = message.temporalData;
            proto.MovementData = message.movementData;
            proto.HeadSyncData = message.headSyncData;
        }

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.TryEnqueue(fullMovementMessage, @for);
        }

        /// <summary>
        ///     For Debug purposes only
        /// </summary>
        public async UniTaskVoid SelfSendWithDelayAsync(NetworkMovementMessage message, float delay)
        {
            if (settingsValue.UseCompression)
            {
                MessageWrap<MovementCompressed> messageWrap = messagePipesHub.IslandPipe().NewMessage<MovementCompressed>();
                WriteToProto(messageEncoder.Compress(message), messageWrap.Payload);

                await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: cancellationTokenSource.Token);

                CompressedNetworkMovementMessage compressedMessage = new ()
                {
                    temporalData = messageWrap.Payload.TemporalData,
                    movementData = messageWrap.Payload.MovementData,
                };

                message = messageEncoder.Decompress(compressedMessage);
            }
            else
            {
                MessageWrap<Decentraland.Kernel.Comms.Rfc4.Movement> messageWrap = messagePipesHub.IslandPipe().NewMessage<Decentraland.Kernel.Comms.Rfc4.Movement>();
                WriteToProto(message, messageWrap.Payload);
                message = UncompressedMovementMessage(messageWrap.Payload);
            }

            Inbox(message, @for: RemotePlayerMovementComponent.TEST_ID);
        }

    }
}
