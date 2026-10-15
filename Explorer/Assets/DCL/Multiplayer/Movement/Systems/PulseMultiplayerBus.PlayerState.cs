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
        private enum ResyncKind : byte
        {
            PacketLoss,
            Promotion,
        }

        private const string PLAYER_STATE_MESSAGE = "player state";
        private const string PLAYER_STATE_DELTA_MESSAGE = "player state delta";

        // Concurrent collections are not needed as messages are processed strictly from a single thread at a time message by message
        private readonly Dictionary<uint, (uint sequence, NetworkMovementMessage message)> lastMovementMessages = new ();
        private readonly Dictionary<uint, ResyncKind> pendingResyncs = new ();

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

            // An empty realm violates the server contract, so it is dropped too
            if (playerJoined.Realm.Length == 0 || playerJoined.Realm != pulseRealm.Value)
            {
                ReportHub.LogWarning(ReportCategory.MULTIPLAYER, $"Dropping PlayerJoined for {playerJoined.State.SubjectId}: realm '{playerJoined.Realm}' differs from current '{pulseRealm.Value}'");
                return;
            }

            Web3Address resolvedWallet = ResolveSelfMirrorWallet(playerJoined.UserId);

            bool knownPeer = peerIdCache.TryGetWallet(playerJoined.State.SubjectId, out Web3Address previousWallet);
            bool peerReassigned = knownPeer && !previousWallet.Equals(resolvedWallet);
            bool replacesSelectedPeer = peerIdCache.TryGetPeerId(resolvedWallet, out uint selectedPeerId) && selectedPeerId != playerJoined.State.SubjectId;

            if (peerReassigned)
                PurgeQueues(playerJoined.State.SubjectId);

            Web3Address? orphanedWallet = peerIdCache.Set(resolvedWallet, playerJoined.State.SubjectId, playerJoined.Realm, out uint? promotedPeerId);

            if (orphanedWallet.HasValue)
            {
                removeIntentions.Enqueue(orphanedWallet.Value);
                RestorePeerEmote(orphanedWallet.Value, playerJoined.State.SubjectId, 0, stopIfInactive: false);
            }
            else if (promotedPeerId.HasValue)
                RestorePromotedPeer(previousWallet, promotedPeerId.Value);

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerJoined.State);

            if (knownPeer && !peerReassigned)
                TryUpdateLastMovementAndCompleteResync(playerJoined.State.ServerTick, playerJoined.State.SubjectId, playerJoined.State.Sequence, movementMessage);
            else
            {
                lastMovementMessages[playerJoined.State.SubjectId] = (playerJoined.State.Sequence, movementMessage);
                pendingResyncs.Remove(playerJoined.State.SubjectId);
            }

            if (peerIdCache.GetWalletInRealm(playerJoined.State.SubjectId, pulseRealm.Value, out _) != PeerIdCache.LookupResult.Found)
                return;

            // Join supersedes any still-queued leave for this wallet; drop the stale remove so it can't delete a present peer.
            removeIntentions.Cancel(resolvedWallet);

            incomingProfiles.Enqueue(resolvedWallet, playerJoined.ProfileVersion);

            movementMessage.resetTimeline = !knownPeer || peerReassigned;
            Inbox(movementMessage, resolvedWallet);

            if (replacesSelectedPeer)
                RestorePeerEmote(resolvedWallet, playerJoined.State.SubjectId, movementMessage.timestamp);
        }

        private void HandlePlayerLeft(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerLeft playerLeft = message.Message.PlayerLeft;

            RemovePeerRegistration(playerLeft.SubjectId);
        }

        private void HandlePlayerStateFull(IncomingMessage message)
        {
            if (isDisposed)
            {
                ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
                return;
            }

            PlayerStateFull playerStateFull = message.Message.PlayerStateFull;

            PeerIdCache.LookupResult lookup = GetPeerInCurrentRealm(playerStateFull.SubjectId, PLAYER_STATE_MESSAGE, out Web3Address wallet);
            if (lookup is not (PeerIdCache.LookupResult.Found or PeerIdCache.LookupResult.SupersededPeer))
                return;

            bool isShadow = lookup == PeerIdCache.LookupResult.SupersededPeer;
            bool resetTimeline = !isShadow && IsPromotionResyncPending(playerStateFull.SubjectId);

            if (resetTimeline && lastMovementMessages.TryGetValue(playerStateFull.SubjectId, out var lastMovement) && playerStateFull.Sequence < lastMovement.sequence)
                return;

            NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerStateFull);
            TryUpdateLastMovementAndCompleteResync(playerStateFull.ServerTick, playerStateFull.SubjectId, playerStateFull.Sequence, movementMessage,
                allowOverrides: resetTimeline, completePromotionResync: true);

            if (isShadow)
                return;

            if (resetTimeline)
            {
                movementMessage.resetTimeline = true;
                movementMessage.isInstant = true;
            }

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

            PeerIdCache.LookupResult lookup = GetPeerInCurrentRealm(delta.SubjectId, PLAYER_STATE_DELTA_MESSAGE, out Web3Address wallet);
            if (lookup is not (PeerIdCache.LookupResult.Found or PeerIdCache.LookupResult.SupersededPeer))
                return;

            bool isShadow = lookup == PeerIdCache.LookupResult.SupersededPeer;
            bool resetTimeline = !isShadow && IsPromotionResyncPending(delta.SubjectId);

            if (!lastMovementMessages.TryGetValue(delta.SubjectId, out (uint sequence, NetworkMovementMessage message) lastMovement))
            {
                if (isShadow)
                    return;

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
                    if (isShadow)
                        return;

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

            if (isShadow)
                return;

            if (resetTimeline)
            {
                movementMessage.resetTimeline = true;
                movementMessage.isInstant = true;
            }

            Inbox(movementMessage, wallet);
        }

        private bool IsPromotionResyncPending(uint subjectId) =>
            pendingResyncs.TryGetValue(subjectId, out ResyncKind kind) && kind == ResyncKind.Promotion;

        private bool TryRequestResync(uint subjectId, uint knownSequence, ResyncKind kind = ResyncKind.PacketLoss)
        {
            if (!pendingResyncs.TryAdd(subjectId, kind)) return false;

            resyncCount++;

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

        private void TryUpdateLastMovementAndCompleteResync(uint serverTick, uint subjectId, uint sequence, NetworkMovementMessage movementMessage,
            bool allowOverrides = false, bool completePromotionResync = false)
        {
            if (lastMovementMessages.TryGetValue(subjectId, out (uint sequence, NetworkMovementMessage message) lastMessage))
            {
                if (lastMessage.sequence < sequence || (allowOverrides && lastMessage.sequence == sequence))
                    lastMovementMessages[subjectId] = (sequence, movementMessage);
            }
            else
                lastMovementMessages[subjectId] = (sequence, movementMessage);

            if (pendingResyncs.TryGetValue(subjectId, out ResyncKind resyncKind) && (resyncKind != ResyncKind.Promotion || completePromotionResync))
            {
                pendingResyncs.Remove(subjectId);
                ReportHub.Log(ReportCategory.MULTIPLAYER, $"[{serverTick}] Packet loss detected, resync requested for {subjectId}. Received seq: {sequence}, Known seq: {lastMessage.sequence}");
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
            state.MovementBlendQuantized = Mathf.Clamp(message.animState.MovementBlendValue, MovementBlend.MIN, MovementBlend.MAX);
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

            float movementBlend = Mathf.Clamp(playerState.MovementBlendQuantized, MovementBlend.MIN, MovementBlend.MAX);
            var movementKind = (MovementKind)Mathf.Max(Mathf.RoundToInt(movementBlend), movementBlend > LiveKitMovementMessageBus.WALK_EPSILON ? 1 : 0);

            bool isPointingAt = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.PointingAt);

            var message = new NetworkMovementMessage
            {
                timestamp = serverTick * SERVER_TICKS_TO_MOVEMENT_TIMESTAMP,
                parcel = parcel,
                position = worldPosition,
                rotationY = playerState.RotationYQuantized,
                velocity = vel,
                velocitySqrMagnitude = vel.sqrMagnitude,
                movementKind = movementKind,

                animState = new AnimationStates
                {
                    MovementBlendValue = movementBlend,
                    SlideBlendValue = playerState.SlideBlendQuantized,
                    IsGrounded = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Grounded),
                    IsLongJump = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.LongJump),
                    IsFalling = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Falling),
                    IsLongFall = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.LongFall),
                    JumpCount = playerState.JumpCount,
                    GlideState = ToNetworkMovementGlideState(playerState.GlideState),
                },
                isStunned = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.Stunned),
                isInstant = isInstant,
                isEmoting = IsSubjectEmoting(subjectId),

                headIKYawEnabled = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.HeadYaw),
                headIKPitchEnabled = EnumUtils.HasFlag(playerState.StateFlags, PlayerAnimationFlags.HeadPitch),
                headYawAndPitch = new Vector2(playerState.HeadYawQuantized, playerState.HeadPitchQuantized),

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
                    return GlideStateValue.ClosingProp;
                case GlideState.Gliding:
                    return GlideStateValue.Gliding;
                case GlideState.OpeningProp:
                    return GlideStateValue.OpeningProp;
                case GlideState.PropClosed:
                    return GlideStateValue.PropClosed;
                default: throw new ArgumentOutOfRangeException(nameof(glideState), glideState, null);
            }
        }
    }
}
