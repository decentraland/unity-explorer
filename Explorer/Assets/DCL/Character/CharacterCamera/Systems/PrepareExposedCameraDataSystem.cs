using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
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
        private readonly CinemachineBrain cinemachineBrain;

        internal PrepareExposedCameraDataSystem(World world, CinemachineBrain cinemachineBrain) : base(world)
        {
            this.cinemachineBrain = cinemachineBrain;
        }

        protected override void Update(float t)
        {
            PrepareQuery(World);
        }

        [Query]
        private void Prepare(ref CameraComponent cameraComponent, ref ExposedCameraData exposedCameraData, in CursorComponent cursorComponent)
        {
            exposedCameraData.CameraMode = cameraComponent.Mode;
            exposedCameraData.CameraType.Value = cameraComponent.Mode.ToSDKCameraType();
            exposedCameraData.PointerIsLocked.Value = cursorComponent.CursorState != CursorState.Free;
            Transform transform = cameraComponent.Camera.transform;
            exposedCameraData.WorldPosition.Value = transform.position;
            exposedCameraData.WorldRotation.Value = transform.rotation;
            exposedCameraData.CinemachineBrain = cinemachineBrain;
        }
    }
}
