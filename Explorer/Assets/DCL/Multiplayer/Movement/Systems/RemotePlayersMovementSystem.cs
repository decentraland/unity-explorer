using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.CharacterMotion.Components;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Profiles.Systems;
using ECS.Abstract;
using UnityEngine;
using Utility.PriorityQueue;
using static DCL.CharacterMotion.Components.CharacterAnimationComponent;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(AvatarInstantiatorSystem))]
    [UpdateAfter(typeof(MultiplayerProfilesSystem))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        // Amount of positions with delta below MinPositionDelta, that will be skip in one frame.
        private const int SAME_POSITION_BATCH = 10;

        // Amount of positions with timestamp that older than timestamp of the last passed message, that will be skip in one frame.
        private const int OLD_MESSAGES_BATCH = 10;
        private const int BEHIND_EXTRAPOLATION_BATCH = 10;

        private readonly IMultiplayerMovementSettings settings;
        private readonly MultiplayerMovementMessageBus messageBus;

        public RemotePlayersMovementSystem(World world, MultiplayerMovementMessageBus messageBus, IMultiplayerMovementSettings settings) : base(world)
        {
            this.settings = settings;
            this.messageBus = messageBus;
        }

        protected override void Update(float t)
        {
            messageBus.InjectWorld(World!);
            UpdateRemotePlayersMovementQuery(World, t);
        }

        private static void HandleFirstMessage(FullMovementMessage firstRemote, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, in IAvatarView view)
        {
            transComp.Transform.position = firstRemote.position;
            UpdateAnimations(firstRemote.animState, firstRemote.isStunned, ref anim, view);

            remotePlayerMovement.AddPassed(firstRemote, wasTeleported: true);
            remotePlayerMovement.Initialized = true;
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemotePlayersMovement(
            [Data] float deltaTime,
            ref CharacterTransform transComp,
            ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement,
            ref InterpolationComponent intComp,
            ref ExtrapolationComponent extComp,
            in IAvatarView view
        )
        {
            var playerInbox = remotePlayerMovement.Queue;
            if (playerInbox == null) return;

            settings.InboxCount = playerInbox.Count;

            // First message
            if (!remotePlayerMovement.Initialized && playerInbox.Count > 0)
            {
                HandleFirstMessage(playerInbox.Dequeue(), ref transComp, ref anim, ref remotePlayerMovement, in view);
                if (playerInbox.Count == 0) return;
            }

            if (intComp.Enabled)
            {
                deltaTime = Interpolate(deltaTime, ref transComp, ref anim, ref remotePlayerMovement, ref intComp, in view);
                if (deltaTime <= 0) return;
            }

            // Filter old messages that arrived too late
            for (var i = 0; i < OLD_MESSAGES_BATCH && playerInbox.Count > 0 && playerInbox.First.timestamp <= remotePlayerMovement.PastMessage.timestamp; i++)
                playerInbox.Dequeue();

            // When there is no messages, we extrapolate
            if (playerInbox.Count == 0 && settings.UseExtrapolation && remotePlayerMovement is { Initialized: true, WasTeleported: false })
            {
                if (!extComp.Enabled)
                    extComp.Restart(from: remotePlayerMovement.PastMessage, settings.ExtrapolationSettings.TotalMoveDuration);

                // TODO (Vit): properly handle animations for Extrapolation: InterpolateAnimations(deltaTime, ref anim, extComp.Start, extComp.Start);
                Extrapolation.Execute(deltaTime, ref transComp, ref extComp, settings.ExtrapolationSettings);

                return;
            }

            if (playerInbox.Count > 0)
                HandleNewMessage(deltaTime, ref transComp, ref anim, ref remotePlayerMovement, ref intComp, ref extComp, view, playerInbox);
        }

        private void HandleNewMessage(float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp,
            ref ExtrapolationComponent extComp, IAvatarView view, SimplePriorityQueue<FullMovementMessage> playerInbox)
        {
            FullMovementMessage remote = playerInbox.Dequeue();
            var isBlend = false;

            if (extComp.Enabled)
            {
                // if we success with stop of extrapolation, then we can start to blend
                if (StopExtrapolationIfCan(ref remote, ref transComp, ref anim, ref remotePlayerMovement, ref extComp, view, playerInbox))
                    isBlend = true;
                else return;
            }

            if (CanTeleport(remotePlayerMovement, remote))
            {
                isBlend = false;
                TeleportFiltered(ref remote, ref transComp, ref anim, ref remotePlayerMovement, view, playerInbox);

                if (playerInbox.Count == 0) return;

                remote = playerInbox.Dequeue();
            }

            StartInterpolation(deltaTime, ref transComp, ref anim, ref remotePlayerMovement, ref intComp, view, remote, isBlend);
        }

        private static bool StopExtrapolationIfCan(ref FullMovementMessage remote, ref CharacterTransform transComp, ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref ExtrapolationComponent extComp, IAvatarView view, SimplePriorityQueue<FullMovementMessage> playerInbox)
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

                var local = new FullMovementMessage
                {
                    timestamp = minExtTimestamp, // we need to take the timestamp that < remote.timestamp

                    position = transComp.Transform.position,
                    velocity = extComp.Start.velocity,

                    animState = extComp.Start.animState,
                    isStunned = extComp.Start.isStunned,
                };

                remotePlayerMovement.AddPassed(local);
                UpdateAnimations(local.animState, local.isStunned, ref anim, view);
            }

            return true;
        }

        private void TeleportFiltered(ref FullMovementMessage remote, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, IAvatarView view,
            SimplePriorityQueue<FullMovementMessage> playerInbox)
        {
            // Filter messages with the same position
            if (settings.InterpolationSettings.UseSpeedUp)
                for (var i = 0; i < SAME_POSITION_BATCH && playerInbox.Count > 0 && Vector3.SqrMagnitude(playerInbox.First.position - remote.position) < settings.MinPositionDelta; i++)
                    remote = playerInbox.Dequeue();

            transComp.Transform.position = remote.position;
            remotePlayerMovement.AddPassed(remote, wasTeleported: true);
            UpdateAnimations(remote.animState, remote.isStunned, ref anim, view);
        }

        private bool CanTeleport(in RemotePlayerMovementComponent remotePlayerMovement, in FullMovementMessage remote) =>
            Vector3.SqrMagnitude(remotePlayerMovement.PastMessage.position - remote.position) > settings.MinTeleportDistance ||
            (settings.InterpolationSettings.UseSpeedUp && Vector3.SqrMagnitude(remotePlayerMovement.PastMessage!.position - remote.position) < settings.MinPositionDelta);

        private void StartInterpolation(float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp,
            IAvatarView view, FullMovementMessage remote, bool isBlend)
        {
            RemotePlayerInterpolationSettings? intSettings = settings.InterpolationSettings;
            intComp.Restart(remotePlayerMovement.PastMessage, remote, intSettings.UseBlend ? intSettings.BlendType : intSettings.InterpolationType);

            if (intSettings.UseBlend && isBlend)
                SlowDownBlend(ref intComp, intSettings.MaxBlendSpeed);
            else if (intSettings.UseSpeedUp)
                SpeedUpForCatchingUp(ref intComp, settings.InboxCount);

            transComp.Transform.position = intComp.Start.position;

            // TODO (Vit): Restart in loop until (unusedTime <= 0) ?
            float unusedTime = Interpolate(deltaTime, ref transComp, ref anim, ref remotePlayerMovement, ref intComp, in view);
        }

        private float Interpolate(float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp,
            in IAvatarView view)
        {
            float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp, settings.InterpolationSettings.LookAtTimeDelta);
            InterpolateAnimations(deltaTime, intComp.TotalDuration, ref anim, intComp.Start.animState, intComp.End.animState);

            if (intComp.Time < intComp.TotalDuration)
                return -1;

            intComp.Stop();
            remotePlayerMovement.AddPassed(intComp.End);
            UpdateAnimations(intComp.End.animState, intComp.End.isStunned, ref anim, view);

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

        private static void InterpolateAnimations(float t, float totalDuration, ref CharacterAnimationComponent anim, AnimationStates startState, AnimationStates endStates)
        {
            anim.States.MovementBlendValue = Mathf.Lerp(startState.MovementBlendValue, endStates.MovementBlendValue, t / totalDuration);
            anim.States.SlideBlendValue = Mathf.Lerp(startState.SlideBlendValue, endStates.SlideBlendValue, t / totalDuration);
        }

        private static void UpdateAnimations(AnimationStates animState, bool isStunned, ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            animationComponent.States = animState;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            if (view.GetAnimatorBool(AnimationHashes.JUMPING))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
