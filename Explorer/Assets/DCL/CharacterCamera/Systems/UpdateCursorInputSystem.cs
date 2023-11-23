using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera.Components;
using DCL.Input;
using DCL.Input.Systems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DCL.CharacterCamera.Systems
{
    [UpdateBefore(typeof(UpdateCameraInputSystem))]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly IEventSystem eventSystem;
        private readonly ICursor cursor;
        private readonly DCLInput.CameraActions cameraActions;
        private readonly DCLInput.UIActions uiActions;

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IEventSystem eventSystem, ICursor cursor) : base(world)
        {
            this.eventSystem = eventSystem;
            this.cursor = cursor;
            cameraActions = dclInput.Camera;
            uiActions = dclInput.UI;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref CameraComponent cameraComponent)
        {
            Vector2 mousePos = Mouse.current.position.value;
            Vector2 controllerDelta = uiActions.ControllerDelta.ReadValue<Vector2>();

            UpdateCursorPositionForControllers(ref cameraComponent, controllerDelta, mousePos);

            bool inputWantsToLock = cameraActions.Lock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasPressedThisFrame();
            bool inputWantsToUnlock = cameraActions.Unlock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasReleasedThisFrame();

            if (inputWantsToLock && !cameraComponent.CursorIsLocked)
            {
                IReadOnlyList<RaycastResult> results = eventSystem.RaycastAll(mousePos);

                if (results.Count == 0)
                {
                    cameraComponent.CursorIsLocked = true;
                    cursor.Lock();
                }
            }
            else if (inputWantsToUnlock && cameraComponent.CursorIsLocked)
                UnlockCursor(ref cameraComponent);

            // in case the cursor was unlocked externally
            if (!cursor.IsLocked() && cameraComponent.CursorIsLocked)
                UnlockCursor(ref cameraComponent);

            return;

            void UnlockCursor(ref CameraComponent cameraComponent)
            {
                cameraComponent.CursorIsLocked = false;
                cursor.Unlock();
                cameraComponent.CursorPosition = mousePos;
            }
        }

        private void UpdateCursorPositionForControllers(ref CameraComponent cameraComponent, Vector2 controllerDelta,
            Vector2 mousePos)
        {
            if (!(controllerDelta.sqrMagnitude > 0) || cameraComponent.CursorIsLocked) return;

            // If we unlock for the first time we update the mouse position
            if (Mathf.Approximately(cameraComponent.CursorPosition.x, 0) &&
                Mathf.Approximately(cameraComponent.CursorPosition.y, 0))
                cameraComponent.CursorPosition = mousePos;

            // Todo: extract the +1 to sensitivity settings for controllers
            float fastCursor = uiActions.ControllerFastCursor.ReadValue<float>() + 1;
            cameraComponent.CursorPosition += controllerDelta * fastCursor;
            Mouse.current.WarpCursorPosition(cameraComponent.CursorPosition);
        }
    }
}
