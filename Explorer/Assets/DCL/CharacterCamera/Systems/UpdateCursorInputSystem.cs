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
    [UpdateBefore(typeof(UpdateCameraInputSystem))]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCursorInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private readonly IUIRaycaster uiRaycaster;
        private readonly ICursor cursor;
        private readonly DCLInput.CameraActions cameraActions;

        internal UpdateCursorInputSystem(World world, DCLInput dclInput, IUIRaycaster uiRaycaster, ICursor cursor) : base(world)
        {
            this.uiRaycaster = uiRaycaster;
            this.cursor = cursor;
            cameraActions = dclInput.Camera;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
        }

        [Query]
        private void UpdateInput(ref CameraComponent cameraComponent)
        {
            Vector2 mousePos = cameraActions.Point.ReadValue<Vector2>();

            bool inputWantsToLock = cameraActions.Lock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasPressedThisFrame();
            bool inputWantsToUnlock = cameraActions.Unlock.WasPerformedThisFrame() || cameraActions.TemporalLock.WasReleasedThisFrame();

            if (inputWantsToLock && !cameraComponent.CursorIsLocked)
            {
                IReadOnlyList<RaycastResult> results = uiRaycaster.RaycastAll(mousePos);

                if (results.Count == 0)
                {
                    cameraComponent.CursorIsLocked = true;
                    cursor.Lock();
                }
            }

            if (inputWantsToUnlock && cameraComponent.CursorIsLocked)
            {
                cameraComponent.CursorIsLocked = false;
                cursor.Unlock();
            }

            // in case the cursor was unlocked externally
            if (!cursor.IsLocked() && cameraComponent.CursorIsLocked)
            {
                cameraComponent.CursorIsLocked = false;
                cursor.Unlock();
            }
        }
    }
}
