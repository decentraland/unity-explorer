using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
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
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [LogCategory(ReportCategory.MOTION)]
    [UpdateAfter(typeof(CameraGroup))]
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
            ref ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform rigidTransform,
            ref TransformComponent transform,
            ref CharacterPlatformComponent platformComponent,
            in StunComponent stunComponent)
        {
            Vector3 moveVelocity = rigidTransform.MoveVelocity.Velocity;

            if (stunComponent.IsStunned)
                moveVelocity = Vector3.zero;

            Transform characterTransform = transform.Transform;
            Vector3 targetForward = moveVelocity;
            targetForward.y = 0;

            if (targetForward.sqrMagnitude < characterTransform.forward.sqrMagnitude)
                targetForward = characterTransform.forward;

            Quaternion targetRotation = Quaternion.LookRotation(targetForward);
            characterTransform.rotation = Quaternion.RotateTowards(characterTransform.rotation, targetRotation, characterControllerSettings.RotationSpeed * dt);

            // TODO: Move this to other System?
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastRotation = platformComponent.CurrentPlatform.transform.InverseTransformDirection(characterTransform.forward);

        }
    }
}
