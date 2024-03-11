using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.RoomHubs;
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
            if (playerInbox.Count > 0 && remotePlayerMovement.PassedMessages.Count == 0)
            {
                FullMovementMessage remote = playerInbox.Dequeue();

                intComp.Transform.position = remote.position;
                AddToPassed(remote, ref remotePlayerMovement, ref anim, view);

                return;
            }

            if (intComp.Enabled)
            {
                float unusedTime = Interpolation.Execute(ref transComp, ref intComp, deltaTime, settings.InterpolationSettings);
                InterpolateAnimations(ref anim, intComp.Start, intComp.End, deltaTime);

                deltaTime = unusedTime;

                // ReSharper disable once CompareOfFloatsByEqualityOperator - we set it exactly equal on end
                if (intComp.Time == intComp.TotalDuration)
                {
                    intComp.Enabled = false;
                    AddToPassed(intComp.End, ref remotePlayerMovement, ref anim, view);
                }

                // we continue logic if we have some unusedTime to consume
                if (deltaTime <= 0) return;
            }

            if (settings.useExtrapolation && playerInbox.Count == 0 && remotePlayerMovement.PassedMessages.Count > 1)
            {
                if (!extComp.Enabled)
                    extComp.Restart(remotePlayerMovement.PassedMessages[^1], settings.ExtrapolationSettings);

                Extrapolation.Execute(ref transComp, ref extComp, deltaTime, settings.ExtrapolationSettings);
                InterpolateAnimations(ref anim, extComp.Start, extComp.Start, deltaTime);

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

                    intComp.Transform.position = remote.position;

                    if(playerInbox.Count == 0)
                        remotePlayerMovement.PassedMessages.Clear(); // reset to 1 message, so Extrapolation do not start (only for zero velocity)

                    AddToPassed(remote, ref remotePlayerMovement, ref anim, view);
                }
                else
                {
                    // Should be in loop until (t <= 0)
                    intComp.Run(remotePlayerMovement.PassedMessages[^1], remote, playerInbox.Count, settings, local != null && settings.useBlend);

                    FullMovementMessage passed = null;

                    float unusedTime = Interpolation.Execute(ref transComp, ref intComp, deltaTime, settings.InterpolationSettings);
                    if (intComp.Time == intComp.TotalDuration)
                    {
                        intComp.Enabled = false;
                        passed = intComp.End;
                    }

                    InterpolateAnimations(ref anim, intComp.Start, intComp.End, deltaTime);

                    if (passed != null)
                        AddToPassed(passed, ref remotePlayerMovement, ref anim, view);
                }
            }
        }

        private static void AddToPassed(FullMovementMessage passed, ref RemotePlayerMovementComponent remotePlayerMovement, ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            remotePlayerMovement.PassedMessages.Add(passed);
            UpdateAnimations(passed, ref anim, view);
        }

        private static void InterpolateAnimations(ref CharacterAnimationComponent anim, FullMovementMessage start, FullMovementMessage end, float t)
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
