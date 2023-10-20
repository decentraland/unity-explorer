using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.AvatarRendering.AvatarShape;
using DCL.CharacterMotion.Animation;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
using ECS.Abstract;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    public partial class CharacterAnimationSystem : BaseUnityLoopSystem
    {
        public CharacterAnimationSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ResolveVelocityQuery(World, t);
        }

        [Query]
        private void ResolveVelocity(
            [Data] float dt,
            ref CharacterAnimationComponent animationComponent,
            in AvatarBase avatarBase,
            in ICharacterControllerSettings characterControllerSettings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput
        )
        {
            ApplyMovementBlend.Execute(dt, ref animationComponent, in characterControllerSettings, in rigidTransform, in movementInput, in avatarBase);
        }
    }
}
