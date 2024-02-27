using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.AvatarRendering.DemoScripts.Systems;
using DCL.Character.Components;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.Movement.MessageBusMock;
using DCL.ParcelsService;
using ECS.Abstract;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Movement.ECS.System
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(InstantiateRandomAvatarsSystem))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class ReceiverMockSystem : BaseUnityLoopSystem
    {
        private readonly MessagePipeMock pipe;

        private ReceiverMockSystem(World world, MessagePipeMock pipe) : base(world)
        {
            this.pipe = pipe;
        }

        protected override void Update(float t)
        {
            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext,
            ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            pipe.Settings.InboxCount = pipe.Count;
            pipe.Settings.PassedMessages = replicaMovement.PassedMessages.Count;

            if (@int.Enabled)
            {
                MessageMock? passed = @int.Update(UnityEngine.Time.deltaTime);

                if (passed != null)
                    AddToPassed(passed, ref replicaMovement, ref anim, view);
                return;
            }

            if (pipe.Count == 0 && replicaMovement.PassedMessages.Count > 1 && pipe.Settings.useExtrapolation)
            {
                if (!ext.Enabled)
                    ext.Run(replicaMovement.PassedMessages[^1], pipe.Settings);

                ext.Update(UnityEngine.Time.deltaTime);
                return;
            }

            if (pipe.Count > 0)
            {
                MessageMock remote = pipe.Dequeue();
                MessageMock local = null;

                if (ext.Enabled)
                {
                    if (remote.timestamp < ext.Start.timestamp + ext.Time)
                        return;

                    local = ext.Stop();
                    AddToPassed(local, ref replicaMovement, ref anim, view);
                }

                if (replicaMovement.PassedMessages.Count == 0
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) < pipe.Settings.MinPositionDelta
                    || Vector3.Distance(replicaMovement.PassedMessages[^1].position, remote.position) > pipe.Settings.MinTeleportDistance)
                {
                    // Teleport
                    @int.Transform.position = remote.position;
                    replicaMovement.PassedMessages.Clear();
                    AddToPassed(remote, ref replicaMovement, ref anim, view);
                }
                else
                {
                    @int.Run(replicaMovement.PassedMessages[^1], remote, pipe.Count, pipe.Settings, local != null && pipe.Settings.useBlend);
                }
            }
        }

        private static void AddToPassed(MessageMock passed,  ref ReplicaMovementComponent replicaMovement, ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            replicaMovement.PassedMessages.Add(passed);
            UpdateAnimations(passed, ref anim, view);
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
