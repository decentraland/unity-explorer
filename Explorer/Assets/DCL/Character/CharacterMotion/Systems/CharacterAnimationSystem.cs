using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using ECS.Abstract;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    public partial class CharacterAnimationSystem : BaseUnityLoopSystem
    {
        public CharacterAnimationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            UpdateAnimationQuery(World, t);
        }

        [Query]
        private void UpdateAnimation(
            [Data] float dt,
            ref CharacterAnimationComponent animationComponent,
            in IAvatarView view,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput,
            in StunComponent stunComponent
        )
        {
            // Update the movement blend value, ranges from 0 to 3 (Idle = 0, Walk = 1, Jog = 2, Run = 3)
            animationComponent.States.MovementBlendValue = AnimationMovementBlendLogic.CalculateBlendValue(dt, animationComponent.States.MovementBlendValue, movementInput.Kind, rigidTransform.MoveVelocity.Velocity.sqrMagnitude, settings);
            AnimationMovementBlendLogic.SetAnimatorParameters(ref animationComponent, view, rigidTransform.IsGrounded, (int)movementInput.Kind);

            // Update slide blend value, ranges from 0 to 1
            animationComponent.IsSliding = AnimationSlideBlendLogic.IsSliding(in rigidTransform, in settings);
            animationComponent.States.SlideBlendValue = AnimationSlideBlendLogic.CalculateBlendValue(dt, animationComponent.States.SlideBlendValue, animationComponent.IsSliding, settings);
            AnimationSlideBlendLogic.SetAnimatorParameters(ref animationComponent, view);

            // Apply other states
            AnimationStatesLogic.Execute(ref animationComponent, view, rigidTransform, in stunComponent, settings);
        }
    }
}
