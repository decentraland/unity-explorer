using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using ECS.Abstract;
using UnityEngine;
using InputAction = UnityEngine.InputSystem.InputAction;

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
        private InputAction pointerDelta;

        internal PrepareExposedCameraDataSystem(World world, CinemachineBrain cinemachineBrain) : base(world)
        {
            this.cinemachineBrain = cinemachineBrain;
        }

        public override void Initialize()
        {
            base.Initialize();

            pointerDelta = DCLInput.Instance.Camera.Delta;
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

            // Accumulated every render frame regardless of lock state: scene-tick-throttled consumers
            // diff two snapshots, so no intermediate frame motion is lost and unconsumed motion is harmless.
            exposedCameraData.AccumulatedPointerDelta += pointerDelta.ReadValue<Vector2>();

            Transform transform = cameraComponent.Camera.transform;
            exposedCameraData.WorldPosition.Value = transform.position;
            exposedCameraData.WorldRotation.Value = transform.rotation;
            exposedCameraData.CinemachineBrain = cinemachineBrain;
        }
    }
}
