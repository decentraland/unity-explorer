using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Landscape.Settings;
using DCL.Multiplayer.Connections.Messaging;
using DCL.Multiplayer.Connections.Messaging.Hubs;
using DCL.Multiplayer.Connections.Messaging.Pipe;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Tables;
using Decentraland.Kernel.Comms.Rfc4;
using Decentraland.Pulse;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
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

        private const float WALK_EPSILON = 0.05f;

        private readonly IMessagePipesHub messagePipesHub;
        private readonly PulseMultiplayerService pulseService;
        private readonly IReadOnlyEntityParticipantTable entityParticipantTable;
        private readonly CancellationTokenSource cancellationTokenSource = new ();
        private readonly World globalWorld;
        private readonly PeerIdCache peerIdCache;
        private readonly Dictionary<uint, (uint sequence, NetworkMovementMessage message)> lastMovementMessages = new ();

        private NetworkMessageEncoder? messageEncoder;
        private bool isDisposed;
        private IMultiplayerMovementSettings? settingsValue;
        private NetworkMovementMessage? lastMovementSent;
        private uint lastSequenceSent;

        public MultiplayerMovementMessageBus(IMessagePipesHub messagePipesHub,
            PulseMultiplayerService pulseService,
            IReadOnlyEntityParticipantTable entityParticipantTable,
            World globalWorld,
            PeerIdCache peerIdCache)
        {
            this.messagePipesHub = messagePipesHub;
            this.pulseService = pulseService;
            this.entityParticipantTable = entityParticipantTable;
            this.globalWorld = globalWorld;
            this.peerIdCache = peerIdCache;
        }

        public void Dispose()
        {
            isDisposed = true;
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        public void InitializeEncoder(MessageEncodingSettings messageEncodingSettings, IMultiplayerMovementSettings settingsValue, LandscapeData landscapeData)
        {
            this.settingsValue = settingsValue;
            messageEncoder = new NetworkMessageEncoder(messageEncodingSettings, landscapeData);
        }

        public void Send(NetworkMovementMessage message)
        {
            WriteAndSend(message, messagePipesHub.IslandPipe());
            WriteAndSend(message, messagePipesHub.ScenePipe());

            var sendMessage = new ClientMessage();
            ITransport.PacketMode packetMode;

            if (lastMovementSent == null)
            {
                packetMode = ITransport.PacketMode.RELIABLE;
                lastMovementSent = message;
                lastSequenceSent = 0;

                sendMessage.PlayerStateFull = new PlayerStateFull
                {
                    State = ToPlayerState(message, 0),
                };
            }
            else
            {
                packetMode = ITransport.PacketMode.UNRELIABLE_UNSEQUENCED;
                lastSequenceSent++;
                sendMessage.PlayerStateDelta = ToPlayerStateDelta(lastMovementSent.Value, message, lastSequenceSent);
            }

            pulseService.Send(sendMessage, packetMode);
        }

        public UniTask SubscribeToIncomingMessagesAsync(CancellationToken ct)
        {
            messagePipesHub.IslandPipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<Decentraland.Kernel.Comms.Rfc4.Movement>(Packet.MessageOneofCase.Movement, OnOldSchemaMessageReceived);
            messagePipesHub.IslandPipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);
            messagePipesHub.ScenePipe().Subscribe<MovementCompressed>(Packet.MessageOneofCase.MovementCompressed, OnMessageReceived);

            return UniTask.WhenAll(SubscribeToPlayerJoinedAsync(ct),
                SubscribeToPlayerStateFullAsync(ct),
                SubscribeToPlayerStateDeltaAsync(ct));
        }

        private async UniTask SubscribeToPlayerJoinedAsync(CancellationToken ct)
        {
            await foreach (PlayerJoined playerJoined in pulseService.SubscribeAsync<PlayerJoined>(ServerMessage.MessageOneofCase.PlayerJoined, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                    break;
                }

                peerIdCache.Set(playerJoined.UserId, playerJoined.State.SubjectId);

                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerJoined.State);
                lastMovementMessages[playerJoined.State.SubjectId] = (playerJoined.State.Sequence, movementMessage);

                Inbox(movementMessage, playerJoined.UserId);
            }
        }

        private async UniTask SubscribeToPlayerStateFullAsync(CancellationToken ct)
        {
            await foreach (PlayerStateFull playerStateFull in pulseService.SubscribeAsync<PlayerStateFull>(ServerMessage.MessageOneofCase.PlayerStateFull, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                    break;
                }

                PlayerState playerState = playerStateFull.State;

                if (!peerIdCache.TryGetWallet(playerState.SubjectId, out string wallet))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving player state from unknown peer {playerState.SubjectId}");
                    continue;
                }

                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerState);

                if (lastMovementMessages.TryGetValue(playerState.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
                {
                    if (lastMessage.sequence < playerState.Sequence)
                        lastMovementMessages[playerState.SubjectId] = (playerState.Sequence, movementMessage);
                }
                else
                    lastMovementMessages[playerState.SubjectId] = (playerState.Sequence, movementMessage);

                Inbox(movementMessage, wallet);
            }
        }

        private async UniTask SubscribeToPlayerStateDeltaAsync(CancellationToken ct)
        {
            await foreach (PlayerStateDeltaTier0 playerState in pulseService.SubscribeAsync<PlayerStateDeltaTier0>(ServerMessage.MessageOneofCase.PlayerStateDelta, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                    break;
                }

                if (!peerIdCache.TryGetWallet(playerState.SubjectId, out string wallet))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving player state from unknown peer {playerState.SubjectId}");
                    continue;
                }

                if (!lastMovementMessages.TryGetValue(playerState.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMovement))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Cannot merge delta player state from {playerState.SubjectId}");
                    continue;
                }

                NetworkMovementMessage movementMessage = MergeIntoNetworkMovementMessage(lastMovement.message, playerState);

                if (lastMovement.sequence < playerState.NewSeq)
                    lastMovementMessages[playerState.SubjectId] = (playerState.NewSeq, movementMessage);

                Inbox(movementMessage, wallet);
            }
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
            TryEnqueue(@for, fullMovementMessage);
            ReportHub.Log(ReportCategory.MULTIPLAYER_MOVEMENT, $"Movement from {@for} - {fullMovementMessage}");
        }

        private void TryEnqueue(string walletId, NetworkMovementMessage fullMovementMessage)
        {
            if (!entityParticipantTable.TryGet(walletId, out IReadOnlyEntityParticipantTable.Entry entry))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER_MOVEMENT, $"Entity for wallet {walletId} not found");
                return;
            }

            Entity entity = entry.Entity;

            if (globalWorld.TryGet(entity, out RemotePlayerMovementComponent remotePlayerMovementComponent))
                remotePlayerMovementComponent.Enqueue(fullMovementMessage);
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

        private static NetworkMovementMessage ToNetworkMovementMessage(PlayerState playerState)
        {
            var vel = new Vector3(playerState.Velocity.X, playerState.Velocity.Y, playerState.Velocity.Z);

            float movementBlend = Mathf.Clamp(playerState.MovementBlend, 0, 3);
            var movementKind = (MovementKind)Mathf.Max(Mathf.RoundToInt(movementBlend), movementBlend > WALK_EPSILON ? 1 : 0);

            var message = new NetworkMovementMessage
            {
                // TODO: timestamp might not be the same as serverTick (?)
                timestamp = playerState.ServerTick,
                position = new Vector3(playerState.Position.X, playerState.Position.Y, playerState.Position.Z),
                rotationY = playerState.RotationY,
                velocity = vel,
                velocitySqrMagnitude = vel.sqrMagnitude,
                movementKind = movementKind,

                animState = new AnimationStates
                {
                    MovementBlendValue = movementBlend,
                    SlideBlendValue = playerState.SlideBlend,
                    IsGrounded = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Grounded),

                    IsLongJump = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.LongJump),
                    IsFalling = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Falling),
                    IsLongFall = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.LongFall),
                },
                isStunned = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Stunned),

                // TODO: resolve instant (?)
                isInstant = false,

                // TODO: resolve emoting
                isEmoting = false,

                headIKYawEnabled = playerState.HasHeadYaw,
                headIKPitchEnabled = playerState.HasHeadPitch,
                headYawAndPitch = new Vector2(playerState.HeadYaw, playerState.HeadPitch),
            };

            return message;
        }

        private static NetworkMovementMessage MergeIntoNetworkMovementMessage(NetworkMovementMessage last, PlayerStateDeltaTier0 delta)
        {
            // TODO: timestamp might not be the same as serverTick (?)
            last.timestamp = delta.ServerTick;

            var velocityChanged = false;
            var movementBlendChanged = false;

            if (delta.HasPositionX) last.position.x = delta.PositionX;
            if (delta.HasPositionY) last.position.y = delta.PositionY;
            if (delta.HasPositionZ) last.position.z = delta.PositionZ;

            if (delta.HasRotationY) last.rotationY = delta.RotationY;

            if (delta.HasVelocityX)
            {
                last.velocity.x = delta.VelocityX;
                velocityChanged = true;
            }

            if (delta.HasVelocityY)
            {
                last.velocity.y = delta.VelocityY;
                velocityChanged = true;
            }

            if (delta.HasVelocityZ)
            {
                last.velocity.z = delta.VelocityZ;
                velocityChanged = true;
            }

            if (velocityChanged)
                last.velocitySqrMagnitude = last.velocity.sqrMagnitude;

            if (delta.HasMovementBlend)
            {
                float movementBlend = Mathf.Clamp(delta.MovementBlend, 0, 3);
                last.animState.MovementBlendValue = movementBlend;
                movementBlendChanged = true;
            }

            if (delta.HasSlideBlend)
                last.animState.SlideBlendValue = delta.SlideBlend;

            if (delta.HasHeadYaw)
                last.headYawAndPitch.x = delta.HeadYaw;

            if (delta.HasHeadPitch)
                last.headYawAndPitch.y = delta.HeadPitch;

            uint flags = delta.StateFlags;

            last.animState.IsGrounded = (flags & (1 << 1)) != 0;

            // TODO Jumping was reworked
            // last.animState.IsJumping = (flags & (1 << 2)) != 0;
            last.animState.IsLongJump = (flags & (1 << 3)) != 0;
            last.animState.IsFalling = (flags & (1 << 4)) != 0;
            last.animState.IsLongFall = (flags & (1 << 5)) != 0;
            last.isStunned = (flags & (1 << 6)) != 0;
            last.headIKYawEnabled = (flags & (1 << 7)) != 0;
            last.headIKPitchEnabled = (flags & (1 << 8)) != 0;

            if (movementBlendChanged)
            {
                float mb = last.animState.MovementBlendValue;

                last.movementKind = (MovementKind)Mathf.Max(
                    Mathf.RoundToInt(mb),
                    mb > WALK_EPSILON ? 1 : 0
                );
            }

            // TODO: isInstant, isEmoting

            return last;
        }

        private static PlayerState ToPlayerState(NetworkMovementMessage message, uint sequence)
        {
            return new PlayerState
            {
                // No need own subjectId, can be resolved on the server
                // SubjectId = subjectId,
                Sequence = sequence,

                // ServerTick = (uint)message.timestamp,

                Position = new Decentraland.Pulse.Vector3
                {
                    X = message.position.x,
                    Y = message.position.y,
                    Z = message.position.z,
                },

                Velocity = new Decentraland.Pulse.Vector3
                {
                    X = message.velocity.x,
                    Y = message.velocity.y,
                    Z = message.velocity.z,
                },

                RotationY = message.rotationY,
                MovementBlend = Mathf.Clamp(message.animState.MovementBlendValue, 0, 3),
                SlideBlend = message.animState.SlideBlendValue,
                HeadYaw = message.headYawAndPitch.x,
                HeadPitch = message.headYawAndPitch.y,
                StateFlags = BuildStateFlags(message),
            };
        }

        private static PlayerStateDelta ToPlayerStateDelta(
            NetworkMovementMessage previous,
            NetworkMovementMessage current,
            uint newSeq)
        {
            var delta = new PlayerStateDelta
            {
                // No need own subjectId, can be resolved on the server
                // SubjectId = subjectId,
                NewSeq = newSeq,
                // ServerTick = (uint)current.timestamp,
            };

            // TODO: use quantizer instead of casting
            if (Changed(previous.position.x, current.position.x))
                delta.PositionX = (uint) current.position.x;

            if (Changed(previous.position.y, current.position.y))
                delta.PositionY = (uint) current.position.y;

            if (Changed(previous.position.z, current.position.z))
                delta.PositionZ = (uint) current.position.z;

            if (Changed(previous.velocity.x, current.velocity.x))
                delta.VelocityX = (uint) current.velocity.x;

            if (Changed(previous.velocity.y, current.velocity.y))
                delta.VelocityY = (uint) current.velocity.y;

            if (Changed(previous.velocity.z, current.velocity.z))
                delta.VelocityZ = (uint) current.velocity.z;

            if (Changed(previous.rotationY, current.rotationY))
                delta.RotationY = (uint) current.rotationY;

            float currentMovementBlend = Mathf.Clamp(current.animState.MovementBlendValue, 0, 3);
            float previousMovementBlend = Mathf.Clamp(previous.animState.MovementBlendValue, 0, 3);

            if (Changed(previousMovementBlend, currentMovementBlend))
                delta.MovementBlend = (uint) currentMovementBlend;

            if (Changed(previous.animState.SlideBlendValue, current.animState.SlideBlendValue))
                delta.SlideBlend = (uint) current.animState.SlideBlendValue;

            if (Changed(previous.headYawAndPitch.x, current.headYawAndPitch.x))
                delta.HeadYaw = (uint) current.headYawAndPitch.x;

            if (Changed(previous.headYawAndPitch.y, current.headYawAndPitch.y))
                delta.HeadPitch = (uint) current.headYawAndPitch.y;

            uint currentFlags = BuildStateFlags(current);

            delta.StateFlags = currentFlags;

            return delta;

            bool Changed(float a, float b, float epsilon = 0.0001f) =>
                Mathf.Abs(a - b) > epsilon;
        }

        private static uint BuildStateFlags(NetworkMovementMessage message)
        {
            uint flags = 0;

            if (message.animState.IsGrounded)
                flags |= 1u << 1;

            // TODO: jumping has been reworked
            // if (message.animState.IsJumping)
            //     flags |= 1u << 2;

            if (message.animState.IsLongJump)
                flags |= 1u << 3;

            if (message.animState.IsFalling)
                flags |= 1u << 4;

            if (message.animState.IsLongFall)
                flags |= 1u << 5;

            if (message.isStunned)
                flags |= 1u << 6;

            if (message.headIKYawEnabled)
                flags |= 1u << 7;

            if (message.headIKPitchEnabled)
                flags |= 1u << 8;

            return flags;
        }
    }
}
