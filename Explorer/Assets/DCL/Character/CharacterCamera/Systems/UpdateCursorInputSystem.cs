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
        private void UpdateInput(ref CursorComponent cursorComponent)
        {
            Vector2 mousePos = Mouse.current.position.value;
            Vector2 controllerDelta = uiActions.ControllerDelta.ReadValue<Vector2>();

            UpdateCursorPosition(ref cursorComponent, controllerDelta, mousePos);

            bool inputWantsToLock = cameraActions.Lock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasPressedThisFrame();
            bool inputWantsToUnlock = cameraActions.Unlock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasReleasedThisFrame();

            if (inputWantsToLock && !cursorComponent.CursorIsLocked)
            {
                IReadOnlyList<RaycastResult> results = eventSystem.RaycastAll(mousePos);

                if (results.Count == 0)
                {
                    cursorComponent.CursorIsLocked = true;
                    cursor.Lock();
                }
            }
            else if (inputWantsToUnlock && cursorComponent.CursorIsLocked)
                UnlockCursor(ref cursorComponent);

            // in case the cursor was unlocked externally
            if (!cursor.IsLocked() && cursorComponent.CursorIsLocked)
                UnlockCursor(ref cursorComponent);

            return;

            void UnlockCursor(ref CursorComponent cursorComponent)
            {
                cursorComponent.CursorIsLocked = false;
                cursor.Unlock();
                cursorComponent.Position = mousePos;
            }
        }

        private void UpdateCursorPosition(ref CursorComponent cursorComponent, Vector2 controllerDelta, Vector2 mousePos)
        {
            if (cursorComponent.CursorIsLocked)
                return;

            if (controllerDelta.sqrMagnitude > 0)
            {
                // If we unlock for the first time we update the mouse position
                if (Mathf.Approximately(cursorComponent.Position.x, 0) &&
                    Mathf.Approximately(cursorComponent.Position.y, 0))
                    cursorComponent.Position = mousePos;

                // Todo: extract the +1 to sensitivity settings for controllers
                float fastCursor = uiActions.ControllerFastCursor.ReadValue<float>() + 1;
                cursorComponent.Position += controllerDelta * fastCursor;
                Mouse.current.WarpCursorPosition(cursorComponent.Position);
            }
            else
                cursorComponent.Position = mousePos;
        }
    }
}
