using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera.Components;
using DCL.Input;
using DCL.Input.Systems;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly IEventSystem eventSystem;
        private readonly DCLInput.CameraActions cameraActions;
        private readonly PointerEventData pointerData;
        private readonly List<RaycastResult> results;

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IEventSystem eventSystem) : base(world)
        {
            this.eventSystem = eventSystem;
            cameraActions = dclInput.Camera;

            // Cache data to avoid allocations
            pointerData = eventSystem.GetPointerEventData();
            results = new List<RaycastResult>();
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref CameraInput cursorInput)
        {
            var mousePos = cameraActions.Point.ReadValue<Vector2>();

            if (cameraActions.Lock.WasPerformedThisFrame() && !cursorInput.IsCursorLocked)
            {
                pointerData.position = mousePos;
                eventSystem.RaycastAll(pointerData, results);

                if (results.Count == 0)
                {
                    cursorInput.IsCursorLocked = true;
                    UpdateLockState(cursorInput.IsCursorLocked);
                }
            }

            if (cameraActions.Unlock.WasPerformedThisFrame() && cursorInput.IsCursorLocked)
            {
                cursorInput.IsCursorLocked = false;
                UpdateLockState(cursorInput.IsCursorLocked);
            }

            // in case the cursor was unlocked externally
            if (Cursor.lockState == CursorLockMode.None)
                cursorInput.IsCursorLocked = false;
        }

        private void UpdateLockState(bool locked)
        {
            if (locked && Cursor.lockState == CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            } else if (!locked && Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
