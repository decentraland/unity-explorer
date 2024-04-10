using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.AUDIO)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class AvatarAudioSystem : BaseUnityLoopSystem
    {

        private AvatarAudioSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
           MonitorIKDataAndSendAudioEventsQuery(World);
        }

        [Query]
        private void MonitorIKDataAndSendAudioEvents(
            ref AvatarBase audioPlaybackController,
            ref CharacterAnimationComponent animationComponent,
            ref FeetIKComponent feetIKComponent,
            in CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings
        )
        {
            if (feetIKComponent.IsDisabled) return;
            
            if (rigidTransform.JustJumped)
            {
                audioPlaybackController.GetAvatarAudioPlaybackController().PlayJumpSound();
                animationComponent.AudioState.IsFalling = true;
            }
            else if (animationComponent.States.IsGrounded)
            {
                if (animationComponent.AudioState.IsFalling)
                {
                    animationComponent.AudioState.IsFalling = false;
                    audioPlaybackController.GetAvatarAudioPlaybackController().PlayLandSound();
                }
                else
                {
                    if (feetIKComponent.Left.IsGrounded && feetIKComponent.Left.WasLifted)
                    {
                        feetIKComponent.Left.WasLifted = false;
                        audioPlaybackController.GetAvatarAudioPlaybackController().PlayStepSound();
                    }
                    else if (feetIKComponent.Right.IsGrounded && feetIKComponent.Right.WasLifted)
                    {
                        feetIKComponent.Right.WasLifted = false;
                        audioPlaybackController.GetAvatarAudioPlaybackController().PlayStepSound();
                    }
                }
            }
            else
            {
                float verticalVelocity = rigidTransform.GravityVelocity.y + rigidTransform.MoveVelocity.Velocity.y;
                animationComponent.AudioState.IsFalling = true;

                if (verticalVelocity < settings.AnimationLongFallSpeed)
                {
                    audioPlaybackController.GetAvatarAudioPlaybackController().PlayLongFallSound();
                } else
                {
                    audioPlaybackController.GetAvatarAudioPlaybackController().PlayShortFallSound();
                }
            }
        }
    }
}
