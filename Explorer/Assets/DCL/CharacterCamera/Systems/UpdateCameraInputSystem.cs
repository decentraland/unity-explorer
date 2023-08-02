using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera.Components;
using ECS.Input.Systems;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateCameraInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly DCLInput.CameraActions cameraActions;

        internal UpdateCameraInputSystem(World world, DCLInput dclInput) : base(world)
        {
            cameraActions = dclInput.Camera;
        }

        protected override void Update(float t)
        {
            // UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref CameraInput inputToUpdate)
        {
            inputToUpdate.ZoomIn = cameraActions.Zoom.ReadValue<Vector2>().y > 0
                                   || cameraActions.ZoomIn.WasPressedThisFrame();

            inputToUpdate.ZoomOut = cameraActions.Zoom.ReadValue<Vector2>().y < 0
                                    || cameraActions.ZoomOut.WasPressedThisFrame();

            inputToUpdate.Axes = cameraActions.Drag.ReadValue<Vector2>();
        }
    }
}
