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
            AnimationMovementBlendLogic.Apply(dt, ref animationComponent, rigidTransform.MoveVelocity.Velocity, rigidTransform.IsGrounded, movementInput.Kind, in view, in settings);

            // Update slide blend value, ranges from 0 to 1
            animationComponent.IsSliding = AnimationSlideBlendLogic.IsSliding(in rigidTransform, in settings);
            AnimationSlideBlendLogic.Apply(dt, ref animationComponent, animationComponent.IsSliding, in view, in settings);

            // Apply other states
            AnimationStatesLogic.Execute(ref animationComponent, in settings, in rigidTransform, in view, in stunComponent);
        }
    }
}
