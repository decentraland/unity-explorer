using CrdtEcsBridge.Components.Conversion;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Pulse;
using DCL.Web3;
using Decentraland.Pulse;
using Pulse.Transport;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using GlideState = Decentraland.Pulse.GlideState;

namespace DCL.Multiplayer.Movement
{
    public partial class PulseMultiplayerBus
    {
        // Concurrent collections are not needed as messages are processed strictly from a single thread at a time message by message
        private readonly Dictionary<uint, (uint sequence, NetworkMovementMessage message)> lastMovementMessages = new ();
        private readonly Dictionary<uint, byte> pendingResyncs = new ();

        public void Send(NetworkMovementMessage message)
        {
            if (isDisposed) return;

            // Always retain the latest pose so a reconnect handshake can assert it via
            // PlayerInitialState — even if the current Pulse session isn't authenticated yet.
            StoreLastMovement(in message);

            if (!pulseService.IsAuthenticated) return;

            // Consecutive input messages are collapsed in ENetTransport

            var clientMessage = OutgoingMessage.Create(PacketMode.UNRELIABLE_SEQUENCED, ClientMessage.MessageOneofCase.Input);
            WritePlayerStateInput(message, clientMessage.Message.Input);

            pulseService.Send(clientMessage);
        }

        private void HandlePlayerJoined(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerJoined playerJoined = message.Message.PlayerJoined;
            Web3Address resolvedWallet = ResolveSelfMirrorWallet(playerJoined.UserId);

            incomingProfiles.Enqueue(resolvedWallet, playerJoined.ProfileVersion);

            peerIdCache.Set(resolvedWallet, playerJoined.State.SubjectId);

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerJoined.State);
            lastMovementMessages[playerJoined.State.SubjectId] = (playerJoined.State.Sequence, movementMessage);

            Inbox(movementMessage, resolvedWallet);
        }

        private void HandlePlayerLeft(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerLeft playerLeft = message.Message.PlayerLeft;

            if (peerIdCache.TryGetWallet(playerLeft.SubjectId, out Web3Address wallet))
                removeIntentions.Enqueue(wallet);

            peerIdCache.Remove(playerLeft.SubjectId);
            lastMovementMessages.Remove(playerLeft.SubjectId);
            pendingResyncs.Remove(playerLeft.SubjectId);
            emotingSubjects.Remove(playerLeft.SubjectId);
        }

        private void HandlePlayerStateFull(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerStateFull playerStateFull = message.Message.PlayerStateFull;

            if (!peerIdCache.TryGetWallet(playerStateFull.SubjectId, out Web3Address wallet))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Receiving player state from unknown peer {playerStateFull.SubjectId}");
                return;
            }

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerStateFull);
            TryUpdateLastMovementAndCompleteResync(playerStateFull.ServerTick, playerStateFull.SubjectId, playerStateFull.Sequence, movementMessage);
            Inbox(movementMessage, wallet);
        }

        private void HandlePlayerStateDelta(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerStateDeltaTier0 delta = message.Message.PlayerStateDelta;

            if (!peerIdCache.TryGetWallet(delta.SubjectId, out Web3Address wallet))
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Receiving player state from unknown peer {delta.SubjectId}");
                return;
            }

            if (!lastMovementMessages.TryGetValue(delta.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMovement))
            {
                if (!TryRequestResync(delta.SubjectId, 0))
                    ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Already waiting for a resync for {delta.SubjectId}");

                return;
            }

            if (delta.NewSeq > lastMovement.sequence)
            {
                // Even if Resync requested, the normal delta still can arrive and "resume" the normal flow, and then the "Resync" request can be received;
                // If compared via "!=" the diff based on the old "resync" request may trigger redundant resync
                if (delta.BaselineSeq > lastMovement.sequence)
                {
                    if (TryRequestResync(delta.SubjectId, lastMovement.sequence))
                        ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Packet loss detected, resync requested for {delta.SubjectId}. Received seq: {delta.NewSeq}, Baseline seq: {delta.BaselineSeq}, Known seq: {lastMovement.sequence}");

                    // If Client should apply deltas optimistically remove this "return"
                    // The corresponding changes on the server will be required
                    return;
                }

                if (delta.BaselineSeq == lastMovement.sequence)
                {
                    // Consecutive seq received, normal flow resumed — clear any stale pending resync
                    // It can be a consecutive delta, or a resync delta - both are valid as there is no order between 2 different channels
                    if (pendingResyncs.Remove(delta.SubjectId))
                        ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Recovered after resync for {delta.SubjectId}. Received seq {delta.NewSeq}, Baseline seq: {delta.BaselineSeq}");
                }
                else
                {
                    // The old "Resync" delta received - ignore it
                    ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Old Resync for {delta.SubjectId}. Received seq: {delta.NewSeq}, Baseline seq: {delta.BaselineSeq}, Known seq: {lastMovement.sequence}");
                    return;
                }
            }
            else
            {
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{delta.ServerTick}] Delta player state received is old {delta.SubjectId}. Received seq: {delta.NewSeq}, Known seq: {lastMovement.sequence}");
                return;
            }

            NetworkMovementMessage movementMessage = MergeIntoNetworkMovementMessage(lastMovement.message, delta);
            lastMovementMessages[delta.SubjectId] = (delta.NewSeq, movementMessage);

            Inbox(movementMessage, wallet);

            return;

            bool TryRequestResync(uint subjectId, uint knownSequence)
            {
                if (!pendingResyncs.TryAdd(subjectId, 0)) return false;

                ResyncCount++;

                OutgoingMessage resyncMessage = OutgoingMessage.Create(PacketMode.RELIABLE,
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

        private void TryUpdateLastMovementAndCompleteResync(uint serverTick, uint subjectId, uint sequence, NetworkMovementMessage movementMessage, bool allowOverrides = false)
        {
            if (lastMovementMessages.TryGetValue(subjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
            {
                if (lastMessage.sequence < sequence || (allowOverrides && lastMessage.sequence == sequence))
                    lastMovementMessages[subjectId] = (sequence, movementMessage);
            }
            else
                lastMovementMessages[subjectId] = (sequence, movementMessage);

            if (pendingResyncs.Remove(subjectId))
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{serverTick}] Packet loss detected, resync requested for {subjectId}. Received seq: {sequence}, Known seq: {lastMessage.sequence}");
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
                float movementBlend = delta.MovementBlendQuantized;
                lastAnimState.MovementBlendValue = movementBlend;
                movementBlendChanged = true;
            }

            if (delta.HasSlideBlend)
                lastAnimState.SlideBlendValue = delta.SlideBlendQuantized;

            if (delta.HasStateFlags)
            {
                uint flags = delta.StateFlags;

                last.headIKYawEnabled = EnumUtils.HasFlag(flags, PlayerAnimationFlags.HeadYaw);
                last.headIKPitchEnabled = EnumUtils.HasFlag(flags, PlayerAnimationFlags.HeadPitch);
                last.isStunned = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Stunned);
                last.isPointingAt = EnumUtils.HasFlag(flags, PlayerAnimationFlags.PointingAt);
                lastAnimState.IsGrounded = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Grounded);
                lastAnimState.IsLongJump = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongJump);
                lastAnimState.IsFalling = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Falling);
                lastAnimState.IsLongFall = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongFall);
            }

            if (delta.HasHeadYaw)
                last.headYawAndPitch.x = delta.HeadYawQuantized;

            if (delta.HasHeadPitch)
                last.headYawAndPitch.y = delta.HeadPitchQuantized;

            if (delta.HasJumpCount)
                lastAnimState.JumpCount = delta.JumpCount;

            if (movementBlendChanged)
            {
                float mb = lastAnimState.MovementBlendValue;

                last.movementKind = (MovementKind)Mathf.Max(
                    Mathf.RoundToInt(mb),
                    mb > LiveKitMovementMessageBus.WALK_EPSILON ? 1 : 0
                );
            }

            if (delta.HasGlideState)
                last.animState.GlideState = ToNetworkMovementGlideState(delta.GlideState);

            if (delta.HasPointAtX) last.pointAtWorldHitPoint.x = delta.PointAtXQuantized;
            if (delta.HasPointAtY) last.pointAtWorldHitPoint.y = delta.PointAtYQuantized;
            if (delta.HasPointAtZ) last.pointAtWorldHitPoint.z = delta.PointAtZQuantized;

            // Delta movements are always interpolated
            last.isInstant = false;

            return last;
        }

        private void WritePlayerStateInput(NetworkMovementMessage message, PlayerStateInput input)
        {
            PlayerState state = input.State ??= new PlayerState();
            WritePlayerState(message, state, parcelEncoder);
        }

        internal static void WritePlayerState(NetworkMovementMessage message, PlayerState state, ParcelEncoder parcelEncoder)
        {
            Vector2Int parcelIndex = message.position.ToParcel();

            var relativePosition = new Vector3(
                message.position.x - (parcelIndex.x * ParcelMathHelper.PARCEL_SIZE),
                message.position.y,
                message.position.z - (parcelIndex.y * ParcelMathHelper.PARCEL_SIZE)
            );

            state.ParcelIndex = parcelEncoder.Encode(parcelIndex);
            state.PositionXQuantized = relativePosition.x;
            state.PositionYQuantized = relativePosition.y;
            state.PositionZQuantized = relativePosition.z;
            state.VelocityXQuantized = message.velocity.x;
            state.VelocityYQuantized = message.velocity.y;
            state.VelocityZQuantized = message.velocity.z;
            state.RotationYQuantized = message.rotationY;
            state.MovementBlendQuantized = Mathf.Clamp(message.animState.MovementBlendValue, 0, 3);
            state.SlideBlendQuantized = message.animState.SlideBlendValue;
            state.StateFlags = BuildStateFlags(message);
            state.GlideState = (GlideState)message.animState.GlideState;
            state.JumpCount = message.animState.JumpCount;

            if (message.headIKYawEnabled)
                state.HeadYawQuantized = message.headYawAndPitch[0];

            if (message.headIKPitchEnabled)
                state.HeadPitchQuantized = message.headYawAndPitch[1];

            if (message.isPointingAt)
            {
                state.PointAtXQuantized = message.pointAtWorldHitPoint.x;
                state.PointAtYQuantized = message.pointAtWorldHitPoint.y;
                state.PointAtZQuantized = message.pointAtWorldHitPoint.z;
            }

        }

        private NetworkMovementMessage ToNetworkMovementMessage(PlayerStateFull full) =>
            ToNetworkMovementMessage(full.State, full.SubjectId, full.ServerTick, false);

        private NetworkMovementMessage ToNetworkMovementMessage(PlayerState playerState, uint subjectId, uint serverTick, bool isInstant)
        {
            Vector2Int parcel = parcelEncoder.Decode(playerState.ParcelIndex);

            var worldPosition = new Vector3(
                (parcel.x * ParcelMathHelper.PARCEL_SIZE) + playerState.PositionXQuantized,
                playerState.PositionYQuantized,
                (parcel.y * ParcelMathHelper.PARCEL_SIZE) + playerState.PositionZQuantized
            );

            var vel = new Vector3(playerState.VelocityXQuantized, playerState.VelocityYQuantized, playerState.VelocityZQuantized);

            float movementBlend = Mathf.Clamp(playerState.MovementBlend, 0, 3);
            var movementKind = (MovementKind)Mathf.Max(Mathf.RoundToInt(movementBlend), movementBlend > LiveKitMovementMessageBus.WALK_EPSILON ? 1 : 0);

            bool isPointingAt = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.PointingAt);

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
                    JumpCount = playerState.JumpCount,
                    GlideState = ToNetworkMovementGlideState(playerState.GlideState),
                },
                isStunned = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Stunned),
                isInstant = isInstant,
                isEmoting = emotingSubjects.Contains(subjectId),

                headIKYawEnabled = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.HeadYaw),
                headIKPitchEnabled = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.HeadPitch),
                headYawAndPitch = new Vector2(playerState.HeadYaw, playerState.HeadPitch),

                isPointingAt = isPointingAt,
                pointAtWorldHitPoint = isPointingAt
                    ? new Vector3(playerState.PointAtXQuantized, playerState.PointAtYQuantized, playerState.PointAtZQuantized)
                    : Vector3.zero,
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

            if (message.headIKYawEnabled)
                flags |= (uint)PlayerAnimationFlags.HeadYaw;

            if (message.headIKPitchEnabled)
                flags |= (uint)PlayerAnimationFlags.HeadPitch;

            if (message.isPointingAt)
                flags |= (uint)PlayerAnimationFlags.PointingAt;

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
                default: throw new ArgumentOutOfRangeException(nameof(glideState), glideState, null);
            }
        }
    }
}
