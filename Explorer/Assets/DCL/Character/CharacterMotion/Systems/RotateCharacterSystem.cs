using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Character.Components;
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
        private RotateCharacterSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            LerpRotationQuery(World, t);
            ForceLookAtQuery(World);
        }

        [Query]
        [None(typeof(PlayerLookAtIntent))]
        private void LerpRotation(
            [Data] float dt,
            ref ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref CharacterTransform transform,
            ref CharacterPlatformComponent platformComponent,
            in StunComponent stunComponent)
        {
            Transform characterTransform = transform.Transform;

            if (Mathf.Approximately(rigidTransform.LookDirection.sqrMagnitude, 0)) return;

            var targetRotation = Quaternion.LookRotation(rigidTransform.LookDirection);

            if (!stunComponent.IsStunned)
                characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, targetRotation, settings.RotationSpeed * dt);

            // If we are on a platform we save our local rotation
            PlatformSaveLocalRotation.Execute(ref platformComponent, characterTransform.forward);
        }

        [Query]
        private void ForceLookAt(in Entity entity, ref CharacterRigidTransform rigidTransform, ref CharacterTransform transform, in PlayerLookAtIntent lookAtIntent)
        {
            // Rotate player to look at camera target
            Vector3 newLookDirection = lookAtIntent.LookAtTarget - transform.Position;
            newLookDirection.y = rigidTransform.LookDirection.y;
            newLookDirection.Normalize();
            rigidTransform.LookDirection = newLookDirection;
            transform.Transform.forward = newLookDirection;

            World.Remove<PlayerLookAtIntent>(entity);
        }
    }
}
