using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Settings;
using DCL.Multiplayer.Movement.System;
using ECS.Abstract;
using UnityEngine;
using Utility.PriorityQueue;

namespace DCL.Multiplayer.Movement.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MULTIPLAYER_MOVEMENT)]
    public partial class RemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerMovementSettings settings;
        private readonly RemotePlayersMovementInbox messagePipe;

        public RemotePlayersMovementSystem(World world, IRoomHub roomHub, IMultiplayerMovementSettings settings) : base(world)
        {
            this.settings = settings;
            messagePipe = new RemotePlayersMovementInbox(roomHub, settings);
        }

        protected override void Update(float t)
        {
            UpdateRemotePlayersMovementQuery(World, t);
        }

        private static void HandleFirstMessage(FullMovementMessage firstRemote,ref CharacterTransform transComp, ref CharacterAnimationComponent anim,ref RemotePlayerMovementComponent remotePlayerMovement, in IAvatarView view)
        {
            transComp.Transform.position = firstRemote.position;
            UpdateAnimations(firstRemote, ref anim, view);

            remotePlayerMovement.AddPassed(firstRemote, wasTeleported: true);
            remotePlayerMovement.Initialized = true;
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemotePlayersMovement([Data] float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp, in IAvatarView view)
        {
            if (!messagePipe.InboxByParticipantMap.TryGetValue(remotePlayerMovement.PlayerWalletId, out SimplePriorityQueue<FullMovementMessage>? playerInbox))
                return;

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
                if (deltaTime < 0) return;
            }

            // Filter old messages that arrived too late
            while (playerInbox.Count > 0 && playerInbox.First.timestamp < remotePlayerMovement.PastMessage.timestamp)
                playerInbox.Dequeue();

            // When there is no messages, we extrapolate
            if (playerInbox.Count == 0 && settings.UseExtrapolation && remotePlayerMovement is { Initialized: true, WasTeleported: false })
            {
                if (!extComp.Enabled)
                    extComp.Restart(from: remotePlayerMovement.PastMessage, settings.ExtrapolationSettings.TotalMoveDuration);

                Extrapolation.Execute(deltaTime, ref transComp, ref extComp, settings.ExtrapolationSettings);
                // TODO (Vit): properly handle animations for Extrapolation: InterpolateAnimations(deltaTime, ref anim, extComp.Start, extComp.Start);
                return;
            }

            // Process new messages
            if (playerInbox.Count > 0)
            {
                FullMovementMessage remote = playerInbox.Dequeue();
                var isBlend = false;

                if (extComp.Enabled)
                {
                    float extStopMovementTimestamp = extComp.Start.timestamp + extComp.TotalMoveDuration; // Player is staying still after that time
                    float extrapolatedTimestamp = extComp.Start.timestamp + extComp.Time; // Total extrapolated timestamp. Can be in move or in stop state (if Time>TotalMoveDuration)

                    float minExtTimestamp = Mathf.Min(extStopMovementTimestamp, extrapolatedTimestamp);

                    // Filter all messages that are behind in time (otherwise we will run back)
                    while (playerInbox.Count > 0 && remote.timestamp < minExtTimestamp)
                        remote = playerInbox.Dequeue();

                    if (remote.timestamp < minExtTimestamp)
                        return;

                    // Stop extrapolating and proceed to blending
                    {
                        isBlend = true;
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
                        UpdateAnimations(local, ref anim, view);
                    }
                }

                // Teleportation
                if (Vector3.SqrMagnitude(remotePlayerMovement.PastMessage.position - remote.position) > settings.MinTeleportDistance ||
                    (settings.InterpolationSettings.UseSpeedUp && Vector3.SqrMagnitude(remotePlayerMovement.PastMessage!.position - remote.position) < settings.MinPositionDelta))
                {
                    isBlend = false;

                    if (settings.InterpolationSettings.UseSpeedUp)
                        while (playerInbox.Count > 0 && Vector3.SqrMagnitude(playerInbox.First.position - remote.position) < settings.MinPositionDelta)
                            remote = playerInbox.Dequeue();

                    transComp.Transform.position = remote.position;
                    remotePlayerMovement.AddPassed(remote, wasTeleported: true);
                    UpdateAnimations(remote, ref anim, view);

                    if (playerInbox.Count == 0)
                        return;

                    remote = playerInbox.Dequeue();
                }

                // Interpolation or blend start
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
            }
        }

        private float Interpolate(float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim, ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, in IAvatarView view)
        {
            float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp, settings.InterpolationSettings.LookAtTimeDelta);
            InterpolateAnimations(deltaTime, ref anim, ref intComp);

            if (intComp.Time < intComp.TotalDuration)
                return -1;

            intComp.Stop();
            remotePlayerMovement.AddPassed(intComp.End);
            UpdateAnimations(intComp.End, ref anim, view);

            return unusedTime;
        }

        private static void SlowDownBlend(ref InterpolationComponent intComp, float maxBlendSpeed)
        {
            float positionDiff = Vector3.Distance(intComp.Start.position, intComp.End.position);
            float speed = positionDiff / intComp.TotalDuration;

            if (speed > maxBlendSpeed)
            {
                float desiredDuration = positionDiff / maxBlendSpeed;
                intComp.SlowDownFactor = desiredDuration / intComp.TotalDuration;
            }
        }

        private void SpeedUpForCatchingUp(ref InterpolationComponent intComp, int inboxMessages)
        {
            float correctionTime = inboxMessages * UnityEngine.Time.smoothDeltaTime;
            intComp.TotalDuration = Mathf.Max(intComp.TotalDuration - correctionTime, intComp.TotalDuration / settings.InterpolationSettings.MaxSpeedUpTimeDivider);
        }

        private static void InterpolateAnimations(float t, ref CharacterAnimationComponent anim, ref InterpolationComponent intComp)
        {
            anim.States.MovementBlendValue = Mathf.Lerp(intComp.Start.animState.MovementBlendValue, intComp.End.animState.MovementBlendValue, t / intComp.TotalDuration);
            anim.States.SlideBlendValue = Mathf.Lerp(intComp.Start.animState.SlideBlendValue, intComp.End.animState.SlideBlendValue, t / intComp.TotalDuration);
        }

        private static void UpdateAnimations(FullMovementMessage fullMovementMessage, ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            animationComponent.States = fullMovementMessage.animState;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            if (view.GetAnimatorBool(AnimationHashes.JUMPING))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, fullMovementMessage.isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
