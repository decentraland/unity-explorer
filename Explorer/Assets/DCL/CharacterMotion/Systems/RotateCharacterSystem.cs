using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Platforms;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    /// <summary>
    ///     Rotates character outside of physics update as it does not impact collisions or any other interactions
    /// </summary>
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public partial class RotateCharacterSystem : BaseUnityLoopSystem
    {
        internal RotateCharacterSystem(World world) : base(world) { }

        public override void Initialize()
        {

        }

        protected override void Update(float t)
        {
            LerpRotationQuery(World, t);
        }

        [Query]
        private void LerpRotation(
            [Data] float dt,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref TransformComponent transform,
            ref CharacterPlatformComponent platformComponent)
        {
            Transform characterTransform = transform.Transform;
            Quaternion targetRotation = GetTargetDirection(rigidTransform.MoveVelocity.Velocity, characterTransform.forward);
            characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, targetRotation, settings.RotationSpeed * dt);

            SaveLocalRotation.Execute(ref platformComponent, characterTransform.forward);
        }

        private static Quaternion GetTargetDirection(Vector3 moveVelocity, Vector3 currentForward)
        {
            Vector3 targetDirection = moveVelocity;
            targetDirection.y = 0;

            if (targetDirection.sqrMagnitude < currentForward.sqrMagnitude)
                targetDirection = currentForward;

            var targetRotation = Quaternion.LookRotation(targetDirection);
            return targetRotation;
        }
    }
}
