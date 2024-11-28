using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using UnityEngine;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        // Amount of positions with timestamp that older than timestamp of the last passed message, that will be skip in one frame.
        private const int BEHIND_EXTRAPOLATION_BATCH = 10;
        private const float ZERO_VELOCITY_SQR_THRESHOLD = 0.01f * 0.01f;

        private readonly IMultiplayerMovementSettings settings;
        private readonly ICharacterControllerSettings characterControllerSettings;

        internal RemotePlayersMovementSystem(World world, IMultiplayerMovementSettings settings, ICharacterControllerSettings characterControllerSettings) : base(world)
        {
            this.settings = settings;
            this.characterControllerSettings = characterControllerSettings;
        }

        protected override void Update(float t)
        {
            UpdateRemotePlayersMovementQuery(World, t);
        }

        private void HandleFirstMessage(ref CharacterTransform transComp, in NetworkMovementMessage firstRemote, ref RemotePlayerMovementComponent remotePlayerMovement)
        {
            transComp.Transform.position = firstRemote.position;

            remotePlayerMovement.AddPassed(firstRemote, characterControllerSettings, wasTeleported: true);
            remotePlayerMovement.Initialized = true;
        }

        [Query]
        [None(typeof(PlayerComponent), typeof(PBAvatarShape), typeof(DeleteEntityIntention))]
        private void UpdateRemotePlayersMovement([Data] float deltaTime, ref CharacterTransform transComp,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp)
        {
            SimplePriorityQueue<NetworkMovementMessage>? playerInbox = remotePlayerMovement.Queue;
            if (playerInbox == null) return;

            settings.InboxCount = playerInbox.Count;

            // First message
            if (!remotePlayerMovement.Initialized && playerInbox.Count > 0)
            {
                HandleFirstMessage(ref transComp, playerInbox.Dequeue(), ref remotePlayerMovement);
                if (playerInbox.Count == 0) return;
            }

            // We wait delay of 2 messages for more stability of interpolation
            if (remotePlayerMovement.InitialCooldownTime < 2 * settings.MoveSendRate)
            {
                remotePlayerMovement.InitialCooldownTime += deltaTime;
                return;
            }

            if (intComp.Enabled)
            {
                deltaTime = Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
                if (deltaTime <= 0) return;
            }

            // Filter old messages that arrived too late
            while (playerInbox.Count > 0 && playerInbox.First.timestamp <= remotePlayerMovement.PastMessage.timestamp)
                playerInbox.Dequeue();

            // When there is no messages, we extrapolate
            if (settings.UseExtrapolation && playerInbox.Count == 0 && remotePlayerMovement is { Initialized: true, WasTeleported: false })
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

        private void HandleNewMessage(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, ref ExtrapolationComponent extComp, SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            NetworkMovementMessage remote = playerInbox.Dequeue();

            var isBlend = false;
            if (extComp.Enabled)
            {
                // if we success with stop of extrapolation, then we can start to blend
                if (TryStopExtrapolation(ref remote, ref transComp, ref remotePlayerMovement, ref extComp, playerInbox))
                    isBlend = true;
                else return;
            }

            if (CanTeleport(remotePlayerMovement, remote))
            {
                isBlend = false;
                TeleportFiltered(ref remote, ref transComp, ref remotePlayerMovement, playerInbox);

                if (playerInbox.Count == 0) return;

                remote = playerInbox.Dequeue();
            }

            StartInterpolation(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp, remote, isBlend);
        }

        private bool TryStopExtrapolation(ref NetworkMovementMessage remote, ref CharacterTransform transComp,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref ExtrapolationComponent extComp, SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            float minExtTimestamp = extComp.Start.timestamp + Mathf.Min(extComp.Time, extComp.TotalMoveDuration);

            // Filter all messages that are behind in time (otherwise we will run back)
            for (var i = 0; i < BEHIND_EXTRAPOLATION_BATCH && playerInbox.Count > 0 && remote.timestamp <= minExtTimestamp; i++)
                remote = playerInbox.Dequeue();

            if (remote.timestamp <= minExtTimestamp)
                return false;

            // Stop extrapolating and proceed to blending
            {
                extComp.Stop();

                var local = new NetworkMovementMessage
                {
                    timestamp = minExtTimestamp, // we need to take the timestamp that < remote.timestamp

                    position = transComp.Transform.position,
                    velocity = extComp.Start.velocity,
                    velocitySqrMagnitude = extComp.Start.velocity.sqrMagnitude,

                    movementKind = extComp.Start.movementKind,
                    isSliding = extComp.Start.isSliding,
                    animState = extComp.Start.animState,
                    isStunned = extComp.Start.isStunned,
                };

                remotePlayerMovement.AddPassed(local, characterControllerSettings);
            }

            return true;
        }

        private void TeleportFiltered(ref NetworkMovementMessage remote, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            SimplePriorityQueue<NetworkMovementMessage> playerInbox)
        {
            // Filter messages with the same position and rotation
            if (settings.InterpolationSettings.UseSpeedUp)
                while (playerInbox.Count > settings.InterpolationSettings.CatchUpMessagesMin
                       && Mathf.Abs(playerInbox.First.rotationY - remote.rotationY) < settings.MinRotationDelta
                       && Vector3.SqrMagnitude(playerInbox.First.position - remote.position) < settings.MinPositionDelta)
                    remote = playerInbox.Dequeue();

            transComp.Transform.position = remote.position;
            remotePlayerMovement.AddPassed(remote, characterControllerSettings, wasTeleported: true);
        }

        private bool CanTeleport(in RemotePlayerMovementComponent remotePlayerMovement, in NetworkMovementMessage remote)
        {
            float posDiff = Vector3.SqrMagnitude(remotePlayerMovement.PastMessage.position - remote.position);
            float rotDiff = Mathf.Abs(remotePlayerMovement.PastMessage.rotationY - remote.rotationY);

            return posDiff > settings.MinTeleportDistance || (settings.InterpolationSettings.UseSpeedUp && rotDiff < settings.MinRotationDelta && posDiff < settings.MinPositionDelta);
        }

        private void StartInterpolation(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp, in NetworkMovementMessage remote, bool isBlend)
        {
            RemotePlayerInterpolationSettings? intSettings = settings.InterpolationSettings;

            bool useLinear = remotePlayerMovement.PastMessage.velocitySqrMagnitude < ZERO_VELOCITY_SQR_THRESHOLD || remote.velocitySqrMagnitude < ZERO_VELOCITY_SQR_THRESHOLD ||
                             remotePlayerMovement.PastMessage.animState.IsGrounded != remote.animState.IsGrounded || remotePlayerMovement.PastMessage.animState.IsJumping != remote.animState.IsJumping
                             || remotePlayerMovement.PastMessage.movementKind == MovementKind.IDLE || remote.movementKind == MovementKind.IDLE;

            // Interpolate linearly to/from zero velocities to avoid position overshooting
            InterpolationType spline = intSettings.UseBlend ? intSettings.BlendType :
                useLinear ? InterpolationType.Linear :
                intSettings.InterpolationType;

            intComp.Restart(remotePlayerMovement.PastMessage, remote, spline, characterControllerSettings);
            AccelerateVerySlowTransition(ref intComp);

            if (intSettings.UseBlend && isBlend)
                SlowDownBlend(ref intComp, intSettings.MaxBlendSpeed);
            else if (intSettings.UseSpeedUp)
                SpeedUpForCatchingUp(ref intComp, settings.InboxCount);

            transComp.Transform.position = intComp.Start.position;

            // TODO (Vit): Restart in loop until (unusedTime <= 0) ?
            float unusedTime = Interpolate(deltaTime, ref transComp, ref remotePlayerMovement, ref intComp);
        }

        private float Interpolate(float deltaTime, ref CharacterTransform transComp, ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp)
        {
            float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp, settings.InterpolationSettings.LookAtTimeDelta, characterControllerSettings.RotationSpeed);

            if (intComp.Time < intComp.TotalDuration)
                return -1;

            intComp.Stop();
            remotePlayerMovement.AddPassed(intComp.End, characterControllerSettings);

            return unusedTime;
        }

        private static void SlowDownBlend(ref InterpolationComponent intComp, float maxBlendSpeed)
        {
            float positionDiff = Vector3.Distance(intComp.Start.position, intComp.End.position);
            float speed = positionDiff / intComp.TotalDuration;

            // we can do more precise, but more expensive solution -  if (speed * speed > Mathf.Max(maxBlendSpeed * maxBlendSpeed, intComp.Start.velocity.sqrMagnitude, intComp.End.velocity.sqrMagnitude))
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

        private void AccelerateVerySlowTransition(ref InterpolationComponent intComp)
        {
            if (intComp.TotalDuration < settings.AccelerationTimeThreshold) return;
            float distance = Vector3.Distance(intComp.Start.position, intComp.End.position);

            MovementKind movementKind = MovementKind.RUN;
            if (distance < settings.MoveKindByDistance[MovementKind.WALK])
                movementKind = MovementKind.WALK;
            else if (distance < settings.MoveKindByDistance[MovementKind.JOG])
                movementKind = MovementKind.JOG;

            float speed = SpeedLimit.Get(characterControllerSettings, movementKind);

            intComp.TotalDuration = Vector3.Distance(intComp.Start.position, intComp.End.position) / speed;
            intComp.UseMessageRotation = false;

            intComp.Start.movementKind = movementKind;
            intComp.Start.animState.MovementBlendValue = (int)movementKind;
            intComp.End.animState.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue( intComp.TotalDuration,  intComp.Start.animState.MovementBlendValue,
                intComp.End.movementKind, intComp.End.velocitySqrMagnitude, characterControllerSettings);
        }
    }
}
