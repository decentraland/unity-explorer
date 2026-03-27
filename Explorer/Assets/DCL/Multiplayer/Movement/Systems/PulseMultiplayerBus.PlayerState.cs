using CrdtEcsBridge.Components.Conversion;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using Decentraland.Pulse;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using GlideState = Decentraland.Pulse.GlideState;

namespace DCL.Multiplayer.Connections.Pulse
{
    public partial class PulseMultiplayerBus
    {
        private readonly HashSet<uint> pendingResyncs = new ();
        private readonly Dictionary<uint, (uint sequence, NetworkMovementMessage message)> lastMovementMessages = new ();

        public void Send(NetworkMovementMessage message)
        {
            // TODO Don't push any messages if connection is not active
            // TODO Override the last movement message in the pipe as it doesn't make sense to send more than 1

            var clientMessage = MessagePipe.OutgoingMessage.Create(ITransport.PacketMode.UNRELIABLE_SEQUENCED, ClientMessage.MessageOneofCase.Input);
            WritePlayerStateInput(message, clientMessage.Message.Input);

            pulseService.Send(clientMessage);
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

                incomingProfiles.Enqueue(playerJoined.UserId, playerJoined.ProfileVersion);

                peerIdCache.Set(playerJoined.UserId, playerJoined.State.SubjectId);

                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerJoined.State);
                lastMovementMessages[playerJoined.State.SubjectId] = (playerJoined.State.Sequence, movementMessage);

                Inbox(movementMessage, playerJoined.UserId);
            }
        }

        private async UniTask SubscribeToPlayerLeftAsync(CancellationToken ct)
        {
            await foreach (PlayerLeft playerLeft in pulseService.SubscribeAsync<PlayerLeft>(ServerMessage.MessageOneofCase.PlayerLeft, ct))
            {
                if (isDisposed)
                {
                    ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                    break;
                }

                if (peerIdCache.TryGetWallet(playerLeft.SubjectId, out string wallet))
                    removeIntentions.Enqueue(wallet);

                peerIdCache.Remove(playerLeft.SubjectId);
                lastMovementMessages.Remove(playerLeft.SubjectId);
                pendingResyncs.Remove(playerLeft.SubjectId);
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

                if (!peerIdCache.TryGetWallet(playerStateFull.SubjectId, out string wallet))
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving player state from unknown peer {playerStateFull.SubjectId}");
                    continue;
                }

                NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerStateFull);

                if (lastMovementMessages.TryGetValue(playerStateFull.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
                {
                    if (lastMessage.sequence < playerStateFull.Sequence)
                        lastMovementMessages[playerStateFull.SubjectId] = (playerStateFull.Sequence, movementMessage);
                }
                else
                    lastMovementMessages[playerStateFull.SubjectId] = (playerStateFull.Sequence, movementMessage);

                pendingResyncs.Remove(playerStateFull.SubjectId);

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
                    if (TryRequestResync(playerState.SubjectId, 0))
                        ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Cannot merge delta player state from {playerState.SubjectId}");

                    continue;
                }

                if (playerState.NewSeq > lastMovement.sequence)
                {
                    if (playerState.NewSeq - lastMovement.sequence > 1)
                    {
                        if (TryRequestResync(playerState.SubjectId, lastMovement.sequence))
                            ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Packet loss detected, resync requested for {playerState.SubjectId}. Received seq: {playerState.NewSeq}, prev seq: {lastMovement.sequence}");
                    }
                    else
                    {
                        // Consecutive seq received, normal flow resumed — clear any stale pending resync
                        pendingResyncs.Remove(playerState.SubjectId);
                    }
                }
                else
                {
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Delta player state received is old {playerState.SubjectId}. Received seq: {playerState.NewSeq}, last seq: {lastMovement.sequence}");
                    continue;
                }

                NetworkMovementMessage movementMessage = MergeIntoNetworkMovementMessage(lastMovement.message, playerState);
                lastMovementMessages[playerState.SubjectId] = (playerState.NewSeq, movementMessage);

                Inbox(movementMessage, wallet);
            }

            return;

            bool TryRequestResync(uint subjectId, uint knownSequence)
            {
                if (!pendingResyncs.Add(subjectId)) return false;

                MessagePipe.OutgoingMessage resyncMessage = MessagePipe.OutgoingMessage.Create(ITransport.PacketMode.RELIABLE,
                    ClientMessage.MessageOneofCase.Resync);

                resyncMessage.Message.Resync = new ResyncRequest
                {
                    SubjectId = subjectId,
                    KnownSeq = knownSequence,
                };

                pulseService.Send(resyncMessage);

                return true;
            }
        }

        private NetworkMovementMessage MergeIntoNetworkMovementMessage(NetworkMovementMessage last, PlayerStateDeltaTier0 delta)
        {
            last.timestamp = delta.ServerTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP;

            var velocityChanged = false;
            var movementBlendChanged = false;

            if (delta.HasParcelIndex) last.parcel = parcelEncoder.Decode(delta.ParcelIndex);

            if (delta.HasPositionX)
                last.position.x = (last.parcel.x * ParcelMathHelper.PARCEL_SIZE) + delta.PositionXQuantized;

            if (delta.HasPositionY)
                last.position.y = delta.PositionYQuantized;

            if (delta.HasPositionZ)
                last.position.z = (last.parcel.y * ParcelMathHelper.PARCEL_SIZE) + delta.PositionZQuantized;

            if (delta.HasRotationY) last.rotationY = delta.RotationYQuantized;

            if (delta.HasVelocityX)
            {
                last.velocity.x = delta.VelocityXQuantized;
                velocityChanged = true;
            }

            if (delta.HasVelocityY)
            {
                last.velocity.y = delta.VelocityYQuantized;
                velocityChanged = true;
            }

            if (delta.HasVelocityZ)
            {
                last.velocity.z = delta.VelocityZQuantized;
                velocityChanged = true;
            }

            if (velocityChanged)
                last.velocitySqrMagnitude = last.velocity.sqrMagnitude;

            ref AnimationStates lastAnimState = ref last.animState;

            if (delta.HasMovementBlend)
            {
                float movementBlend = Mathf.Clamp(delta.MovementBlendQuantized, 0, 3);
                lastAnimState.MovementBlendValue = movementBlend;
                movementBlendChanged = true;
            }

            if (delta.HasSlideBlend)
                lastAnimState.SlideBlendValue = delta.SlideBlendQuantized;

            if (delta.HasHeadYaw)
            {
                last.headYawAndPitch.x = delta.HeadYawQuantized;
                last.headIKYawEnabled = true;
            }

            if (delta.HasHeadPitch)
            {
                last.headYawAndPitch.y = delta.HeadPitchQuantized;
                last.headIKPitchEnabled = true;
            }

            uint flags = delta.StateFlags;

            bool wasGrounded = lastAnimState.IsGrounded;

            lastAnimState.IsGrounded = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Grounded);
            lastAnimState.IsLongJump = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongJump);
            lastAnimState.IsFalling = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Falling);
            lastAnimState.IsLongFall = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongFall);
            last.isStunned = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Stunned);

            if (wasGrounded && !lastAnimState.IsGrounded)
                lastAnimState.JumpCount++;

            if (movementBlendChanged)
            {
                float mb = lastAnimState.MovementBlendValue;

                last.movementKind = (MovementKind)Mathf.Max(
                    Mathf.RoundToInt(mb),
                    mb > MultiplayerMovementMessageBus.WALK_EPSILON ? 1 : 0
                );
            }

            if (delta.HasGlideState)
                last.animState.GlideState = ToNetworkMovementGlideState(delta.GlideState);

            // TODO: isInstant, isEmoting

            return last;
        }

        private void WritePlayerStateInput(NetworkMovementMessage message, PlayerStateInput input)
        {
            PlayerState state = input.State ??= new PlayerState();

            Vector2Int parcelIndex = message.position.ToParcel();

            var relativePosition = new Vector3(
                message.position.x - (parcelIndex.x * ParcelMathHelper.PARCEL_SIZE),
                message.position.y,
                message.position.z - (parcelIndex.y * ParcelMathHelper.PARCEL_SIZE)
            );

            state.ParcelIndex = parcelEncoder.Encode(parcelIndex);
            state.Position = relativePosition.ToProtoVector();
            state.Velocity = message.velocity.ToProtoVector();
            state.RotationY = message.rotationY;
            state.MovementBlend = Mathf.Clamp(message.animState.MovementBlendValue, 0, 3);
            state.SlideBlend = message.animState.SlideBlendValue;
            state.StateFlags = BuildStateFlags(message);
            state.GlideState = (GlideState)message.animState.GlideState;

            if (message.headIKYawEnabled)
                state.HeadYaw = message.headYawAndPitch[0];

            if (message.headIKPitchEnabled)
                state.HeadPitch = message.headYawAndPitch[1];
        }

        private NetworkMovementMessage ToNetworkMovementMessage(PlayerStateFull full) =>
            ToNetworkMovementMessage(full.State, full.ServerTick, false);

        private NetworkMovementMessage ToNetworkMovementMessage(PlayerState playerState, uint serverTick, bool isInstant, bool isEmoting = false)
        {
            Vector2Int parcel = parcelEncoder.Decode(playerState.ParcelIndex);

            var worldPosition = new Vector3(
                (parcel.x * ParcelMathHelper.PARCEL_SIZE) + playerState.Position.X,
                playerState.Position.Y,
                (parcel.y * ParcelMathHelper.PARCEL_SIZE) + playerState.Position.Z
            );

            Vector3 vel = playerState.Velocity.ToUnityVector();

            float movementBlend = Mathf.Clamp(playerState.MovementBlend, 0, 3);
            var movementKind = (MovementKind)Mathf.Max(Mathf.RoundToInt(movementBlend), movementBlend > MultiplayerMovementMessageBus.WALK_EPSILON ? 1 : 0);

            var message = new NetworkMovementMessage
            {
                timestamp = serverTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP,
                parcel = parcel,
                position = worldPosition,
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
                isInstant = isInstant,
                isEmoting = isEmoting,

                headIKYawEnabled = playerState.HasHeadYaw,
                headIKPitchEnabled = playerState.HasHeadPitch,
                headYawAndPitch = new Vector2(playerState.HeadYaw, playerState.HeadPitch),
            };

            return message;
        }

        private static uint BuildStateFlags(NetworkMovementMessage message)
        {
            uint flags = 0;

            if (message.animState.IsGrounded)
                flags |= (uint)PlayerAnimationFlags.Grounded;

            if (message.animState.IsLongJump)
                flags |= (uint)PlayerAnimationFlags.LongJump;

            if (message.animState.IsFalling)
                flags |= (uint)PlayerAnimationFlags.Falling;

            if (message.animState.IsLongFall)
                flags |= (uint)PlayerAnimationFlags.LongFall;

            if (message.isStunned)
                flags |= (uint)PlayerAnimationFlags.Stunned;

            return flags;
        }

        private static GlideStateValue ToNetworkMovementGlideState(GlideState glideState)
        {
            switch (glideState)
            {
                case GlideState.ClosingProp:
                    return GlideStateValue.CLOSING_PROP;
                case GlideState.Gliding:
                    return GlideStateValue.GLIDING;
                case GlideState.OpeningProp:
                    return GlideStateValue.OPENING_PROP;
                case GlideState.PropClosed:
                    return GlideStateValue.PROP_CLOSED;
                default: throw new System.ArgumentOutOfRangeException(nameof(glideState), glideState, null);
            }
        }
    }
}