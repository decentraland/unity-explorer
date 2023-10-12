using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera.Components;
using DCL.Input;
using DCL.Input.Systems;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    [UpdateBefore(typeof(UpdateCameraInputSystem))]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly IUIRaycaster uiRaycaster;
        private readonly DCLInput.CameraActions cameraActions;

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IUIRaycaster uiRaycaster) : base(world)
        {
            this.uiRaycaster = uiRaycaster;
            cameraActions = dclInput.Camera;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref CameraComponent cameraComponent)
        {
            var mousePos = cameraActions.Point.ReadValue<Vector2>();

            if (cameraActions.Lock.WasPerformedThisFrame() && !cameraComponent.CursorIsLocked)
            {
                var results = uiRaycaster.RaycastAll(mousePos);

                if (results.Count == 0)
                {
                    cameraComponent.CursorIsLocked = true;
                    UpdateLockState(cameraComponent.CursorIsLocked);
                }
            }

            if (cameraActions.Unlock.WasPerformedThisFrame() && cameraComponent.CursorIsLocked)
            {
                cameraComponent.CursorIsLocked = false;
                UpdateLockState(cameraComponent.CursorIsLocked);
            }

            // in case the cursor was unlocked externally
            if (Cursor.lockState == CursorLockMode.None)
                cameraComponent.CursorIsLocked = false;
        }

        private void UpdateLockState(bool locked)
        {
            if (locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            } else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
