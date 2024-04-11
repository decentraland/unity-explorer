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
        private SingleInstanceEntity fixedTick;

        private AvatarAudioSystem(World world) : base(world) { }

        public override void Initialize()
        {
            base.Initialize();
            fixedTick = World.CachePhysicsTick();
        }

        protected override void Update(float t)
        {
           MonitorIKDataAndSendAudioEventsQuery(World, fixedTick.GetPhysicsTickComponent(World).Tick);
        }

        [Query]
        private void MonitorIKDataAndSendAudioEvents(
            [Data] int physicsTick,
            ref IAvatarView audioPlaybackController,
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
                    if (physicsTick - animationComponent.AudioState.LastSoundFrame > 20)
                    {
                        if (feetIKComponent.Left is { IsGrounded: true, WasLifted: true })
                        {
                            animationComponent.AudioState.LastSoundFrame = physicsTick;
                            feetIKComponent.Left.WasLifted = false;
                            audioPlaybackController.GetAvatarAudioPlaybackController().PlayStepSound();
                        }
                        else if (feetIKComponent.Right is { IsGrounded: true, WasLifted: true })
                        {
                            animationComponent.AudioState.LastSoundFrame = physicsTick;
                            feetIKComponent.Right.WasLifted = false;
                            audioPlaybackController.GetAvatarAudioPlaybackController().PlayStepSound();
                        }
                    }
                }
            }
            else
            {
                if (physicsTick - animationComponent.AudioState.LastSoundFrame  > 5)
                {
                    animationComponent.AudioState.LastSoundFrame = physicsTick;

                    float verticalVelocity = rigidTransform.GravityVelocity.y + rigidTransform.MoveVelocity.Velocity.y;
                    animationComponent.AudioState.IsFalling = true;

                    if (verticalVelocity < settings.AnimationLongFallSpeed) { audioPlaybackController.GetAvatarAudioPlaybackController().PlayLongFallSound(); }
                    else { audioPlaybackController.GetAvatarAudioPlaybackController().PlayShortFallSound(); }
                }
            }
        }
    }
}
