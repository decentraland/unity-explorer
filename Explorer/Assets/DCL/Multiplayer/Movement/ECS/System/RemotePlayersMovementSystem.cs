using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Multiplayer.Connections.Archipelago.Rooms;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.Multiplayer.Movement.Settings;
using ECS.Abstract;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    // [LogCategory(ReportCategory.AVATAR)]
    public partial class RemotePlayersMovementSystem : BaseUnityLoopSystem
    {
        private readonly IMultiplayerSpatialStateSettings settings;
        private readonly RemotePlayersMovementInbox messagePipe;

        public RemotePlayersMovementSystem(World world, IArchipelagoIslandRoom room, IMultiplayerSpatialStateSettings settings) : base(world)
        {
            this.settings = settings;
            messagePipe = new RemotePlayersMovementInbox(room, settings);

            messagePipe.InitializeAsync().Forget();
        }

        protected override void Update(float t)
        {
            UpdateRemotePlayersMovementQuery(World,t);
        }

        private void InterpolateAnimations(ref CharacterAnimationComponent anim, MessageMock start, MessageMock end, float t)
        {
            float timeDiff = end.timestamp - start.timestamp;

            anim.States.MovementBlendValue = Mathf.Lerp(start.animState.MovementBlendValue, end.animState.MovementBlendValue, t/timeDiff);
            anim.States.SlideBlendValue = Mathf.Lerp(start.animState.SlideBlendValue, end.animState.SlideBlendValue, t/timeDiff);
        }

        [Query]
        [None(typeof(PlayerComponent))]
        private void UpdateRemotePlayersMovement([Data] float t, ref RemotePlayerMovementComponent remotePlayerMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext, in IAvatarView view)
        {
            if(!messagePipe.InboxByParticipantMap.TryGetValue(remotePlayerMovement.PlayerWalletId, out var playerInbox))
                return;

            settings.PassedMessages = remotePlayerMovement.PassedMessages.Count;
            settings.InboxCount = playerInbox.Count;

            CharacterAnimationComponent anim = new CharacterAnimationComponent();

            if (@int.Enabled)
            {
                MessageMock? passed = @int.Update(t);
                // InterpolateAnimations(ref anim, @int.Start, @int.End, t);
                if (passed != null) AddToPassed(passed, ref remotePlayerMovement, ref anim, view);

                return;
            }

            // if (playerInbox.Count < 2)  return;
            if (settings.useExtrapolation && playerInbox.Count == 0 && remotePlayerMovement.PassedMessages.Count > 1)
            {
                if (!ext.Enabled)
                    ext.Run(remotePlayerMovement.PassedMessages[^1], settings);

                ext.Update(t);
                return;
            }

            if (playerInbox.Count > 0)
            {
                MessageMock remote = playerInbox.Dequeue();
                MessageMock local = null;

                if (ext.Enabled)
                {
                    if (remote.timestamp < ext.Start.timestamp + ext.Time || remote.timestamp < ext.Start.timestamp + ext.TotalMoveDuration)
                        return;

                    local = ext.Stop();
                    AddToPassed(local, ref remotePlayerMovement, ref anim, view);
                }

                if (remotePlayerMovement.PassedMessages.Count == 0
                    || Vector3.SqrMagnitude(remotePlayerMovement.PassedMessages[^1].position - remote.position) < settings.MinPositionDelta
                    // || Vector3.Distance(remotePlayerMovement.PassedMessages[^1].position, remote.position) > settings.MinTeleportDistance
                    )
                {
                    // Teleport

                    for (var i = 0; i < settings.SamePositionTeleportFilterCount; i++)
                    {
                        var next = playerInbox.Peek();

                        if (Vector3.SqrMagnitude(next.position - remote.position) < settings.MinPositionDelta)
                        {
                            AddToPassed(remote, ref remotePlayerMovement, ref anim, view);
                            remote = playerInbox.Dequeue();
                        }
                        else
                            break;
                    }

                    @int.Transform.position = remote.position;
                    remotePlayerMovement.PassedMessages.Clear(); // reset to 1 message, so Extrapolation do not start
                    AddToPassed(remote, ref remotePlayerMovement, ref anim, view);
                }
                else
                    @int.Run(remotePlayerMovement.PassedMessages[^1], remote, playerInbox.Count, settings, local != null && settings.useBlend);
            }
        }



        private static void AddToPassed(MessageMock passed, ref RemotePlayerMovementComponent remotePlayerMovement, ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            remotePlayerMovement.PassedMessages.Add(passed);
            // UpdateAnimations(passed, ref anim, view);
        }

        private static void UpdateAnimations(MessageMock message, ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            animationComponent.States = message.animState;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            if (view.GetAnimatorBool(AnimationHashes.JUMPING))
                view.SetAnimatorTrigger(AnimationHashes.JUMP);

            view.SetAnimatorBool(AnimationHashes.STUNNED, message.isStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
