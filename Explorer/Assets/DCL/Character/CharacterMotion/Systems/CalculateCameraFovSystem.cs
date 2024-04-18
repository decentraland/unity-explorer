using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.Diagnostics;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterMotion.Systems
{
    [LogCategory(ReportCategory.MOTION)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraGroup))]
    public partial class CalculateCameraFovSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity camera;

        internal CalculateCameraFovSystem(World world) : base(world) { }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            InterpolateQuery(World, t, ref camera.GetCameraFovComponent(World));
        }

        [Query]
        private void Interpolate(
            [Data] float dt,
            [Data] ref CameraFieldOfViewComponent fieldOfViewComponent,
            in ICharacterControllerSettings characterControllerSettings,
            in CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput)
        {
            if (movementInput.Kind == MovementKind.Run)
            {
                float speedFactor = rigidTransform.MoveVelocity.Velocity.magnitude / characterControllerSettings.RunSpeed;
                float targetFov = Mathf.Lerp(0, characterControllerSettings.CameraFOVWhileRunning, speedFactor);

                fieldOfViewComponent.AdditiveFov = Mathf.MoveTowards(fieldOfViewComponent.AdditiveFov, targetFov, characterControllerSettings.FOVIncreaseSpeed * dt);
            }
            else
                fieldOfViewComponent.AdditiveFov = Mathf.MoveTowards(fieldOfViewComponent.AdditiveFov, 0, characterControllerSettings.FOVDecreaseSpeed * dt);
        }
    }
}
