using CrdtEcsBridge.Components.Conversion;
using Cysharp.Threading.Tasks;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Movement.Systems;
using DCL.Utilities.Extensions;
using Decentraland.Pulse;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility;
using GlideState = Decentraland.Pulse.GlideState;

namespace DCL.Multiplayer.Connections.Pulse
{
    public class PulseMultiplayerBus : IDisposable
    {
        private readonly PulseMultiplayerService pulseService;
        private readonly PeerIdCache peerIdCache;
        private readonly MovementInbox movementInbox;
        private readonly HashSet<uint> pendingResyncs = new ();
        private readonly Dictionary<uint, (uint sequence, NetworkMovementMessage message)> lastMovementMessages = new ();
        private readonly ParcelEncoder parcelEncoder;

        private bool isDisposed;

        public PulseMultiplayerBus(PulseMultiplayerService pulseService, PeerIdCache peerIdCache, MovementInbox movementInbox, ParcelEncoder parcelEncoder)
        {
            this.pulseService = pulseService;
            this.peerIdCache = peerIdCache;
            this.movementInbox = movementInbox;
            this.parcelEncoder = parcelEncoder;
        }

        public void Send(NetworkMovementMessage message)
        {
            // TODO Don't push any messages if connection is not active
            // TODO Override the last movement message in the pipe as it doesn't make sense to send more than 1

            var clientMessage = MessagePipe.OutgoingMessage.Create(ITransport.PacketMode.UNRELIABLE_SEQUENCED, ClientMessage.MessageOneofCase.Input);
            WritePlayerStateInput(message, clientMessage.Message.Input);

            pulseService.Send(clientMessage);
        }

        public void Dispose()
        {
            isDisposed = true;
        }

        public void SubscribeToIncomingMessages(CancellationToken ct)
        {
            // TODO draining and processing should be moved from the main thread, and guarded by the concurrent collections
            // TODO Inboxing messages is the only part that should execute on the main thread
            UniTask.WhenAll(SubscribeToPlayerJoinedAsync(ct),
                        SubscribeToPlayerStateFullAsync(ct),
                        SubscribeToPlayerStateDeltaAsync(ct))
                   .SuppressToResultAsync(ReportCategory.MULTIPLAYER)
                   .Forget();
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

        // private async UniTask SubscribeToPlayerLeftAsync(CancellationToken ct)
        // {
        //     await foreach (PlayerLeft playerLeft in pulseService.SubscribeAsync<PlayerLeft>(ServerMessage.MessageOneofCase.PlayerLeft, ct))
        //     {
        //         if (isDisposed)
        //         {
        //             ReportHub.LogError(ReportCategory.MULTIPLAYER, "Receiving a message while disposed");
        //             break;
        //         }
        //
        //         peerIdCache.Remove(playerLeft.SubjectId);
        //
        //         NetworkMovementMessage movementMessage = ToNetworkMovementMessage(playerLeft.State);
        //         lastMovementMessages[playerLeft.State.SubjectId] = (playerLeft.State.Sequence, movementMessage);
        //
        //         Inbox(movementMessage, playerLeft.UserId);
        //     }
        // }

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

                        continue;
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

        private void Inbox(NetworkMovementMessage fullMovementMessage, string @for)
        {
            movementInbox.TryEnqueue(fullMovementMessage, @for);
        }

        private NetworkMovementMessage MergeIntoNetworkMovementMessage(NetworkMovementMessage last, PlayerStateDeltaTier0 delta)
        {
            last.timestamp = delta.ServerTick;

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

            if (delta.HasMovementBlend)
            {
                float movementBlend = Mathf.Clamp(delta.MovementBlendQuantized, 0, 3);
                last.animState.MovementBlendValue = movementBlend;
                movementBlendChanged = true;
            }

            if (delta.HasSlideBlend)
                last.animState.SlideBlendValue = delta.SlideBlendQuantized;

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

            last.animState.IsGrounded = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Grounded);
            last.animState.IsLongJump = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongJump);
            last.animState.IsFalling = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Falling);
            last.animState.IsLongFall = EnumUtils.HasFlag(flags, PlayerAnimationFlags.LongFall);
            last.isStunned = EnumUtils.HasFlag(flags, PlayerAnimationFlags.Stunned);

            if (movementBlendChanged)
            {
                float mb = last.animState.MovementBlendValue;

                last.movementKind = (MovementKind)Mathf.Max(
                    Mathf.RoundToInt(mb),
                    mb > MultiplayerMovementMessageBus.WALK_EPSILON ? 1 : 0
                );
            }

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

        private NetworkMovementMessage ToNetworkMovementMessage(PlayerStateFull full)
        {
            PlayerState playerState = full.State;

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
                timestamp = full.ServerTick,
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

                // Instant is always false as it was created to simulate Teleportation which is handled separately with the server
                isInstant = false,

                // TODO: resolve emoting
                isEmoting = false,

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
    }
}
