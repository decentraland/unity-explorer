using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Systems;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCameraInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly DCLInput.CameraActions cameraActions;
        private readonly DCLInput.FreeCameraActions freeCameraActions;

        internal UpdateCameraInputSystem(World world, DCLInput dclInput) : base(world)
        {
            cameraActions = dclInput.Camera;
            freeCameraActions = dclInput.FreeCamera;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        [None(typeof(MovementBlockerComponent))]
        private void UpdateInput(ref CameraInput cameraInput, ref CameraComponent cameraComponent, in CursorComponent cursorComponent)
        {
            cameraInput.ZoomIn = cameraActions.Zoom.ReadValue<Vector2>().y > 0
                                 || cameraActions.ZoomIn.WasPressedThisFrame();

            cameraInput.ZoomOut = cameraActions.Zoom.ReadValue<Vector2>().y < 0
                                  || cameraActions.ZoomOut.WasPressedThisFrame();

            cameraInput.Delta = cursorComponent.CursorIsLocked ? cameraActions.Delta.ReadValue<Vector2>() : Vector2.zero;

            cameraInput.FreeMovement = freeCameraActions.Movement.ReadValue<Vector2>();

            if (freeCameraActions.Sprint.IsPressed()) { cameraInput.FreeMovement *= 10; }
        }
    }
}
