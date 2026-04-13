using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Movement.Settings;
using DCL.Utilities;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Systems
{
    /// <summary>
    ///     Pulse-tailored variant of <see cref="RemotePlayersMovementSystem" />.
    ///     The original system was written for LiveKit P2P, which has no delivery ordering and no authoritative
    ///     teleport signal. Under Pulse:
    ///     <list type="bullet">
    ///         <item>Inbound deltas are sequenced per-peer (gaps trigger a Resync), so in-order, non-duplicate delivery is guaranteed.</item>
    ///         <item>Teleports are explicit (<see cref="NetworkMovementMessage.isInstant" /> is true only on the authoritative Teleported envelope).</item>
    ///         <item>Server tick rate bounds the cadence between snapshots, so no "snapshot took forever" recovery heuristic is needed.</item>
    ///     </list>
    ///     Consequently this system removes:
    ///     timestamp-based old-message filtering, the distance-heuristic teleport (CanTeleport),
    ///     the same-pos/rot dedup loop, the AccelerateVerySlowTransition path
    ///     (which forced <c>UseMessageRotation = false</c> — incorrect when rotation is authoritative),
    ///     and the point-at IK plumbing (Pulse's PlayerState carries no point-at fields).
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class PulseRemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerMovementSettings settings;
        private readonly ICharacterControllerSettings characterControllerSettings;

        internal PulseRemotePlayersMovementSystem(World world, IMultiplayerMovementSettings settings, ICharacterControllerSettings characterControllerSettings) : base(world)
        {
            this.settings = settings;
            this.characterControllerSettings = characterControllerSettings;
        }

        protected override void Update(float t)
        {
            UpdateRemotePlayersMovementQuery(World, t);
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateRemotePlayersMovement([Data] float deltaTime,
            ref CharacterTransform transComp,
            ref HeadIKComponent headIK,
            ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp,
            ref ExtrapolationComponent extComp)
        {
            SimplePriorityQueue<NetworkMovementMessage, double>? playerInbox = remotePlayerMovement.Queue;
            if (playerInbox == null) return;

            settings.InboxCount = playerInbox.Count;

            // First snapshot (PlayerJoined / PlayerStateFull) — snap.
            if (!remotePlayerMovement.Initialized && playerInbox.Count > 0)
            {
                HandleFirstMessage(ref transComp, ref headIK, playerInbox.Dequeue(), ref remotePlayerMovement);
                if (playerInbox.Count == 0) return;
            }

            // One server-tick of buffer guarantees we can always interpolate Start → End.
            // (P2P used 2× as a hedge against jitter; under fixed server tick a single tick is sufficient.)
            if (remotePlayerMovement.InitialCooldownTime < settings.MoveSendRate)
            {
                remotePlayerMovement.InitialCooldownTime += deltaTime;
                return;
            }

            // Head-IK lerp runs every frame regardless of motion state.
            Vector2 headYawAndPitch = Vector2.zero;

            if (remotePlayerMovement.HeadIKYawEnabled || remotePlayerMovement.HeadIKPitchEnabled)
                headYawAndPitch = Interpolation.InterpolateHeadIK(headIK, remotePlayerMovement.HeadIKYawAndPitch, settings.InterpolationSettings.HeadIKInterpolationFactor);

            ApplyHeadIK(ref headIK, remotePlayerMovement.HeadIKYawEnabled, remotePlayerMovement.HeadIKPitchEnabled, headYawAndPitch);

            if (intComp.Enabled)
            {
                deltaTime = Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
                if (deltaTime <= 0) return;
            }

            // Extrapolate across brief packet drops — bounded; will be cancelled the moment the next delta arrives.
            if (settings.UseExtrapolation && playerInbox.Count == 0
                                          && remotePlayerMovement is { Initialized: true, WasTeleported: false })
            {
                float sqrMinSpeed = settings.ExtrapolationSettings.MinSpeed * settings.ExtrapolationSettings.MinSpeed;

                if (!extComp.Enabled && remotePlayerMovement.PastMessage.velocitySqrMagnitude > sqrMinSpeed)
                    extComp.Restart(from: remotePlayerMovement.PastMessage, settings.ExtrapolationSettings.TotalMoveDuration);

                if (extComp.Enabled)
                {
                    Extrapolation.Execute(deltaTime, ref transComp, ref extComp, settings.ExtrapolationSettings);
                    return;
                }
            }

            if (playerInbox.Count > 0)
                HandleNewMessage(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp, ref extComp, playerInbox);
        }

        private void HandleFirstMessage(ref CharacterTransform transComp, ref HeadIKComponent headIK, in NetworkMovementMessage firstRemote,
            ref RemotePlayerMovementComponent remotePlayerMovement)
        {
            SetPositionAndRotation(ref transComp, firstRemote.position, firstRemote.rotationY);
            ApplyHeadIK(ref headIK, firstRemote.headIKYawEnabled, firstRemote.headIKPitchEnabled, firstRemote.headYawAndPitch);

            remotePlayerMovement.AddPassed(firstRemote, characterControllerSettings, wasTeleported: true);
            remotePlayerMovement.UpdateHeadIK(firstRemote);
            remotePlayerMovement.Initialized = true;
        }

        private void HandleNewMessage(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, ref ExtrapolationComponent extComp, SimplePriorityQueue<NetworkMovementMessage, double> playerInbox)
        {
            NetworkMovementMessage remote = playerInbox.Dequeue();
            remotePlayerMovement.UpdateHeadIK(remote);

            var isBlend = false;

            if (extComp.Enabled)
            {
                StopExtrapolation(ref transComp, ref remotePlayerMovement, ref extComp);
                isBlend = true;
            }

            // Authoritative server teleport — snap and mark, no interpolation.
            if (remote.isInstant)
            {
                SetPositionAndRotation(ref transComp, remote.position, remote.rotationY);
                remotePlayerMovement.AddPassed(remote, characterControllerSettings, wasTeleported: true);
                return;
            }

            StartInterpolation(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp, remote, isBlend);
        }

        private void StopExtrapolation(ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement, ref ExtrapolationComponent extComp)
        {
            // Capture the extrapolated point as the new "past" so the upcoming interpolation starts from where the avatar visually is.
            double pastTimestamp = extComp.Start.timestamp + Mathf.Min(extComp.Time, extComp.TotalMoveDuration);
            extComp.Stop();

            var local = new NetworkMovementMessage
            {
                timestamp = pastTimestamp,

                position = transComp.Transform.position,
                velocity = extComp.Start.velocity,
                velocitySqrMagnitude = extComp.Start.velocity.sqrMagnitude,

                movementKind = extComp.Start.movementKind,
                isSliding = extComp.Start.isSliding,
                animState = extComp.Start.animState,
                isStunned = extComp.Start.isStunned,
                isEmoting = extComp.Start.isEmoting,
            };

            remotePlayerMovement.AddPassed(local, characterControllerSettings);
        }

        private void StartInterpolation(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, in NetworkMovementMessage remote, bool isBlend)
        {
            RemotePlayerInterpolationSettings intSettings = settings.InterpolationSettings;

            // Linear avoids overshoot at zero-velocity boundaries and across grounded/jump transitions.
            bool useLinear = remotePlayerMovement.PastMessage.velocitySqrMagnitude < RemotePlayerUtils.ZERO_VELOCITY_SQR_THRESHOLD
                             || remote.velocitySqrMagnitude < RemotePlayerUtils.ZERO_VELOCITY_SQR_THRESHOLD
                             || remotePlayerMovement.PastMessage.animState.IsGrounded != remote.animState.IsGrounded
                             || remotePlayerMovement.PastMessage.animState.JumpCount != remote.animState.JumpCount
                             || remotePlayerMovement.PastMessage.movementKind == MovementKind.IDLE
                             || remote.movementKind == MovementKind.IDLE;

            InterpolationType spline = intSettings.UseBlend && isBlend ? intSettings.BlendType :
                useLinear ? InterpolationType.Linear : intSettings.InterpolationType;

            intComp.Restart(remotePlayerMovement.PastMessage, remote, spline, characterControllerSettings);

            // NOTE: UseMessageRotation is left at its default (true). Server rotation is authoritative — never override it.

            // Cap duration by the snapshots' implied speed: prevents visible slow-mo crawl when timestamp gap
            // is large but displacement is moderate (idle-then-move, long pause between deltas, etc.).
            CapDurationToImpliedSpeed(ref intComp);

            if (intSettings.UseBlend && isBlend)
                SlowDownBlend(ref intComp, intSettings.MaxBlendSpeed);
            else if (intSettings.UseSpeedUp)
                SpeedUpForCatchingUp(ref intComp, settings.InboxCount);

            SetPositionAndRotation(ref transComp, intComp.Start.position, intComp.Start.rotationY);

            Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
        }

        private float Interpolate(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp)
        {
            float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp,
                settings.InterpolationSettings.LookAtTimeDelta, characterControllerSettings.RotationSpeed);

            if (intComp.Time < intComp.TotalDuration)
                return -1;

            intComp.Stop();
            remotePlayerMovement.AddPassed(intComp.End, characterControllerSettings);

            return unusedTime;
        }

        private static void SetPositionAndRotation(ref CharacterTransform transformComp, Vector3 position, float rotationY)
        {
            var newRotation = Quaternion.Euler(transformComp.Transform.rotation.x, rotationY, transformComp.Transform.rotation.z);
            transformComp.SetPositionAndRotationWithDirtyCheck(position, newRotation);
        }

        /// <summary>
        ///     Replacement for the legacy <c>AccelerateVerySlowTransition</c>. The duration produced by
        ///     <see cref="InterpolationComponent.Restart" /> equals the *server-time gap* between snapshots —
        ///     when that gap is much longer than the actual motion would take (idle player, sparse delta cadence,
        ///     post-resync), the avatar visibly crawls.
        ///     Pick a "natural" reference speed for traversing this displacement and shorten the duration to match
        ///     when the original is slower. Reference speed is the higher of:
        ///     <list type="bullet">
        ///         <item>the snapshots' own reported velocity (a fast player gets fast-played-back motion), and</item>
        ///         <item>
        ///             a fallback speed implied by the displacement magnitude (a player who visibly moved
        ///             a meter+ between snapshots almost certainly was *running through* that interval — replaying
        ///             it at a near-zero reported velocity would look wrong).
        ///         </item>
        ///     </list>
        ///     Unlike the legacy version this does not override rotation and does not mutate Start blend values.
        /// </summary>
        private void CapDurationToImpliedSpeed(ref InterpolationComponent intComp)
        {
            float distance = Vector3.Distance(intComp.Start.position, intComp.End.position);
            if (distance < RemotePlayerUtils.MOVEMENT_EPSILON) return;

            float startSpeed = Mathf.Sqrt(intComp.Start.velocitySqrMagnitude);
            float endSpeed = Mathf.Sqrt(intComp.End.velocitySqrMagnitude);
            float reportedSpeed = Mathf.Max(startSpeed, endSpeed);

            // Distance-binned fallback: anchors visual pace to a realistic movement kind even when both endpoints
            // report ~0 velocity. Bins mirror the legacy MoveKindByDistance configuration (1 m / 2 m thresholds).
            float fallbackSpeed = distance < 1f ? characterControllerSettings.WalkSpeed :
                distance < 2f ? characterControllerSettings.JogSpeed : characterControllerSettings.RunSpeed;

            float referenceSpeed = Mathf.Max(reportedSpeed, fallbackSpeed);

            float maxDuration = distance / referenceSpeed;
            if (intComp.TotalDuration <= maxDuration) return;

            intComp.TotalDuration = maxDuration;

            // Re-derive the End animation blend value because Restart computed it against the original duration.
            intComp.End.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(
                intComp.TotalDuration,
                intComp.Start.animState.MovementBlendValue,
                intComp.End.movementKind,
                intComp.End.velocitySqrMagnitude,
                characterControllerSettings);
        }

        private static void SlowDownBlend(ref InterpolationComponent intComp, float maxBlendSpeed)
        {
            float positionDiff = Vector3.Distance(intComp.Start.position, intComp.End.position);
            float speed = positionDiff / intComp.TotalDuration;

            if (speed > maxBlendSpeed)
                intComp.TotalDuration = positionDiff / maxBlendSpeed;
        }

        private void SpeedUpForCatchingUp(ref InterpolationComponent intComp, int inboxMessages)
        {
            if (inboxMessages > settings.InterpolationSettings.CatchUpMessagesMin)
            {
                float correctionTime = inboxMessages * UnityEngine.Time.smoothDeltaTime;
                intComp.TotalDuration = Mathf.Max(intComp.TotalDuration - correctionTime, intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
            }
        }

        private static void ApplyHeadIK(ref HeadIKComponent headIK, bool yawEnabled, bool pitchEnabled, Vector2 angles)
        {
            headIK.SetEnabled(yawEnabled, pitchEnabled);
            headIK.LookAt = angles.sqrMagnitude > 0.0001f ? Quaternion.Euler(angles.y, angles.x, 0) * Vector3.forward : Vector3.forward;
        }
    }
}
