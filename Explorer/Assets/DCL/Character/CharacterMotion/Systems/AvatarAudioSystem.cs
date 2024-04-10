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
           //MonitorIKDataAndSendAudioEventsQuery(World);
        }

        [Query]
        private void MonitorIKDataAndSendAudioEvents(
            ref AvatarBase audioPlaybackController,
            in FeetIKComponent feetIKComponent,
            in CharacterRigidTransform rigidTransform,
            in ICharacterControllerSettings settings,
            in CharacterAnimationComponent animationComponent
        )
        {
            //We probably need a component that registers these things and measures time so we dont repeat sounds too soon
            if (feetIKComponent.IsDisabled) return;
            if (rigidTransform.JustJumped)
            {
                audioPlaybackController.GetAvatarAudioPlaybackController().PlayJumpSound();
            }
            else if (animationComponent.States.IsGrounded)
            {
                if (rigidTransform.LastGroundedFrame - rigidTransform.LastJumpFrame < 1)
                {
                    audioPlaybackController.GetAvatarAudioPlaybackController().PlayLandSound();
                }
                else
                {
                    if (feetIKComponent.Left.IsGrounded)
                    {
                    }
                }
            }
            else
            {
                float verticalVelocity = rigidTransform.GravityVelocity.y + rigidTransform.MoveVelocity.Velocity.y;

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
