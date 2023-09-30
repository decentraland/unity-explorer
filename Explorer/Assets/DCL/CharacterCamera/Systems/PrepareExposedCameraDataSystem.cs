using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Prepares data for exposing once in the global world
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    public partial class PrepareExposedCameraDataSystem : BaseUnityLoopSystem
    {
        internal PrepareExposedCameraDataSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            PrepareQuery(World);
        }

        [Query]
        private void Prepare(ref CameraComponent cameraComponent, ref ExposedCameraData exposedCameraData)
        {
            exposedCameraData.CameraType = cameraComponent.Mode.ToSDKCameraType();
            exposedCameraData.PointerIsLocked = cameraComponent.CursorIsLocked;
            Transform transform = cameraComponent.Camera.transform;
            exposedCameraData.WorldPosition = transform.position;
            exposedCameraData.WorldRotation = transform.rotation;
        }
    }
}
