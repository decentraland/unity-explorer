using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
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
            in AvatarBase avatarBase,
            in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput
        )
        {
            ApplyMovementBlend.Execute(dt, ref animationComponent, in settings, in rigidTransform, in movementInput, in avatarBase);
            ApplyJumpState.Execute(ref animationComponent, in settings, in rigidTransform, in avatarBase);
        }
    }
}
