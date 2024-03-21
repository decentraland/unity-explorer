using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Multiplayer.Movement;
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
            UpdateRemotePlayersAnimationQuery(World);
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
            ApplyAnimationMovementBlend.Execute(dt, ref animationComponent, in settings, in rigidTransform, in movementInput, in view);

            // Update slide blend value, ranges from 0 to 1
            ApplyAnimationSlideBlend.Execute(dt, ref animationComponent, in rigidTransform, in view, in settings);

            // Apply other states
            ApplyAnimationState.Execute(ref animationComponent, in settings, in rigidTransform, in view, in stunComponent);
        }

        [Query]
        [All(typeof(RemotePlayerMovementComponent))]
        private void UpdateRemotePlayersAnimation(ref CharacterAnimationComponent animationComponent, in IAvatarView view)
        {
            ApplyAnimationState.ExecuteEmote(ref animationComponent, in view);
        }
    }
}
