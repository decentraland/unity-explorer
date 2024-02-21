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
        private const float MIN_POSITION_DELTA = 0.1f;
        private readonly MessagePipeMock incomingMessages;

        private ReceiverMockSystem(World world, MessagePipeMock incomingMessages) : base(world)
        {
            this.incomingMessages = incomingMessages;
        }

        protected override void Update(float t)
        {
            UpdateInterpolationQuery(World);
        }

        [Query]
        private void UpdateInterpolation(ref ReplicaMovementComponent replicaMovement, ref InterpolationComponent @int, ref ExtrapolationComponent ext, ref BlendComponent blend,
            ref CharacterAnimationComponent anim, in IAvatarView view)
        {
            if (@int.PassedMessages.Count > 0)
                UpdateAnimations(@int.PassedMessages[^1], ref anim, view);

            if (@int.Enabled)
                @int.Update(UnityEngine.Time.deltaTime);
            else
            {
                if (incomingMessages.Count != 0)
                {
                    if (ext.Enabled)
                    {
                        @int.PassedMessages.Add(ext.Stop());

                        // MessageMock? local = ext.Stop();
                        // MessageMock? remote = incomingMessages.Dequeue();
                        //
                        // if (Vector3.Distance(local.position, remote.position) < MIN_POSITION_DELTA)
                        //     blend.Run(local, remote);
                        // else
                        //     @int.PassedMessages.Add(remote);
                    }

                    // if (blend.Enabled)
                    // {
                    //     (MessageMock startedRemote, MessageMock extra) = blend.Update(UnityEngine.Time.deltaTime);
                    //
                    //     if (blend.Enabled) return;
                    //
                    //     @int.PassedMessages.Add(startedRemote);
                    //     if (extra != null) @int.PassedMessages.Add(extra);
                    // }

                    MessageMock? start = @int.PassedMessages.Count > 0 ? @int.PassedMessages[^1] : null;

                    @int.Run(start, incomingMessages.Dequeue(), incomingMessages.Count, incomingMessages.InterpolationType);
                    @int.Update(UnityEngine.Time.deltaTime);
                }
                else
                {
                    if (ext.Enabled)
                        ext.Update(UnityEngine.Time.deltaTime);
                    else if (@int.PassedMessages.Count > 1)
                    {
                        ext.Run(@int.PassedMessages[^1]);
                        ext.Update(UnityEngine.Time.deltaTime);
                    }
                }
            }
        }

        private void UpdateAnimations(MessageMock message, ref CharacterAnimationComponent animationComponent, IAvatarView view)
        {
            animationComponent.States = message.animState;

            view.SetAnimatorFloat(AnimationHashes.MOVEMENT_BLEND, animationComponent.States.MovementBlendValue);
            view.SetAnimatorFloat(AnimationHashes.SLIDE_BLEND, animationComponent.States.SlideBlendValue);

            // view.SetAnimatorBool(AnimationHashes.STUNNED, stunComponent.IsStunned);
            view.SetAnimatorBool(AnimationHashes.GROUNDED, animationComponent.States.IsGrounded);
            view.SetAnimatorBool(AnimationHashes.JUMPING, animationComponent.States.IsJumping);
            view.SetAnimatorBool(AnimationHashes.FALLING, animationComponent.States.IsFalling);
            view.SetAnimatorBool(AnimationHashes.LONG_JUMP, animationComponent.States.IsLongJump);
            view.SetAnimatorBool(AnimationHashes.LONG_FALL, animationComponent.States.IsLongFall);
        }
    }
}
