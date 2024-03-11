using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Movement.Components;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]

    // [LogCategory(ReportCategory.AVATAR)]
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

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemotePlayersMovement([Data] float deltaTime, ref CharacterTransform transComp, ref CharacterAnimationComponent anim,
            ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent intComp, ref ExtrapolationComponent extComp, in IAvatarView view)
        {
            if (!messagePipe.InboxByParticipantMap.TryGetValue(remotePlayerMovement.PlayerWalletId, out Queue<FullMovementMessage>? playerInbox))
                return;

            settings.PassedMessages = remotePlayerMovement.PassedMessages.Count;
            settings.InboxCount = playerInbox.Count;

            // First message
            if (remotePlayerMovement.PassedMessages.Count == 0 && playerInbox.Count > 0)
            {
                FullMovementMessage remote = playerInbox.Dequeue();

                transComp.Transform.position = remote.position;
                AddToPassed(remote, ref remotePlayerMovement, ref anim, view);

                return;
            }

            if (intComp.Enabled)
            {
                float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp);
                InterpolateAnimations(deltaTime, ref anim, intComp.Start, intComp.End);

                if (intComp.Time < intComp.TotalDuration)
                    return;

                // Stop interpolation
                intComp.Stop();
                AddToPassed(intComp.End, ref remotePlayerMovement, ref anim, view);

                deltaTime = unusedTime;
            }

            if (settings.useExtrapolation && playerInbox.Count == 0 && remotePlayerMovement.PassedMessages.Count > 1)
            {
                if (!extComp.Enabled)
                    extComp.Restart(remotePlayerMovement.PassedMessages[^1],
                        settings.ExtrapolationSettings.LinearTime + (settings.ExtrapolationSettings.LinearTime * settings.ExtrapolationSettings.DampedSteps));

                Extrapolation.Execute(deltaTime, ref transComp, ref extComp, settings.ExtrapolationSettings);
                InterpolateAnimations(deltaTime, ref anim, extComp.Start, extComp.Start);

                return;
            }

            if (playerInbox.Count > 0)
            {
                FullMovementMessage remote = playerInbox.Dequeue();
                FullMovementMessage local = null;

                if (extComp.Enabled && (remote.timestamp > extComp.Start.timestamp + extComp.TotalMoveDuration || remote.timestamp > extComp.Start.timestamp + extComp.Time))
                {
                    extComp.Stop();

                    local = new FullMovementMessage
                    {
                        timestamp = remote.timestamp > extComp.Start.timestamp + extComp.TotalMoveDuration
                            ? extComp.Start.timestamp + extComp.TotalMoveDuration
                            : extComp.Start.timestamp + extComp.Time,

                        position = transComp.Transform.position,
                        velocity = extComp.Start.velocity,

                        animState = extComp.Start.animState,
                        isStunned = extComp.Start.isStunned,
                    };

                    AddToPassed(local, ref remotePlayerMovement, ref anim, view);
                }

                if (Vector3.SqrMagnitude(remotePlayerMovement.PassedMessages[^1].position - remote.position) < settings.MinPositionDelta
                    || Vector3.Distance(remotePlayerMovement.PassedMessages[^1].position, remote.position) > settings.MinTeleportDistance)
                {
                    // Teleport
                    for (var i = 0; i < settings.SamePositionTeleportFilterCount && i < playerInbox.Count; i++)
                    {
                        FullMovementMessage? next = playerInbox.Peek();

                        if (Vector3.SqrMagnitude(next.position - remote.position) < settings.MinPositionDelta)
                        {
                            AddToPassed(remote, ref remotePlayerMovement, ref anim, view);
                            remote = playerInbox.Dequeue();
                        }
                        else
                            break;
                    }

                    transComp.Transform.position = remote.position;

                    if (playerInbox.Count == 0)
                        remotePlayerMovement.PassedMessages.Clear(); // reset to 1 message, so Extrapolation do not start (only for zero velocity)

                    AddToPassed(remote, ref remotePlayerMovement, ref anim, view);
                }
                else if (remotePlayerMovement.PassedMessages[^1]?.timestamp < remote.timestamp)
                {
                    RemotePlayerInterpolationSettings? intSettings = settings.InterpolationSettings;

                    // Should be we loop until (unusedTime <= 0) ?
                    intComp.Restart(remotePlayerMovement.PassedMessages[^1], remote, intSettings.UseBlend ? intSettings.BlendType : intSettings.InterpolationType);
                    BlendAndCatchUpTimeCorrection(ref intComp,  intSettings.UseBlend && local != null, playerInbox.Count);

                    transComp.Transform.position = intComp.Start.position;
                    Interpolation.LookAt(ref transComp, intComp.Start.velocity);

                    float unusedTime = Interpolation.Execute(deltaTime, ref transComp, ref intComp);
                    InterpolateAnimations(deltaTime, ref anim, intComp.Start, intComp.End);

                    if (intComp.Time < intComp.TotalDuration)
                        return;

                    intComp.Stop();
                    AddToPassed(intComp.End, ref remotePlayerMovement, ref anim, view);
                }
            }
        }

        private void BlendAndCatchUpTimeCorrection(ref InterpolationComponent intComp, bool isBlend, int inboxMessages)
        {
            var intSettings = settings.InterpolationSettings;

            if (isBlend)
            {
                float positionDiff = Vector3.Distance(intComp.Start.position, intComp.End.position);
                float speed = positionDiff / intComp.TotalDuration;

                if (speed > intSettings.MaxBlendSpeed)
                {
                    float desiredDuration = positionDiff / intSettings.MaxBlendSpeed;
                    intComp.SlowDownFactor = desiredDuration / intComp.TotalDuration;
                }
            }
            else
            {
                float correctionTime = (intSettings.SpeedUpFactor + inboxMessages) * UnityEngine.Time.smoothDeltaTime;
                intComp.TotalDuration = Mathf.Max(intComp.TotalDuration - correctionTime, intComp.TotalDuration / 4f);
            }
        }

        private static void AddToPassed(FullMovementMessage passed, ref RemotePlayerMovementComponent remotePlayerMovement, ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            remotePlayerMovement.PassedMessages.Add(passed);
            UpdateAnimations(passed, ref anim, view);
        }

        private static void InterpolateAnimations(float t, ref CharacterAnimationComponent anim, FullMovementMessage start, FullMovementMessage end)
        {
            float timeDiff = end.timestamp - start.timestamp;

            anim.States.MovementBlendValue = Mathf.Lerp(start.animState.MovementBlendValue, end.animState.MovementBlendValue, t / timeDiff);
            anim.States.SlideBlendValue = Mathf.Lerp(start.animState.SlideBlendValue, end.animState.SlideBlendValue, t / timeDiff);
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
