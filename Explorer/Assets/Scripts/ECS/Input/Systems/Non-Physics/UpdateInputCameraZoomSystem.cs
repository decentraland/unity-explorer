using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ECS.Input.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class UpdateInputCameraZoomSystem : UpdateInputSystem<CameraZoomComponent>
    {

        private readonly InputAction zoomInAction;
        private readonly InputAction zoomOutAction;
        private readonly InputAction zoomWheelAction;

        public UpdateInputCameraZoomSystem(World world, DCLInput dclInput) : base(world)
        {
            zoomInAction = dclInput.Player.ZoomIn;
            zoomOutAction = dclInput.Player.ZoomOut;
            zoomWheelAction = dclInput.Player.Zoom;
        }


        protected override void UpdateInput(ref CameraZoomComponent inputToUpdate)
        {
            inputToUpdate.DoZoomIn = zoomWheelAction.ReadValue<Vector2>().y > 0  || zoomInAction.WasPressedThisFrame();
            inputToUpdate.DoZoomOut = zoomWheelAction.ReadValue<Vector2>().y < 0  ||zoomOutAction.WasPressedThisFrame();
        }
    }
}
