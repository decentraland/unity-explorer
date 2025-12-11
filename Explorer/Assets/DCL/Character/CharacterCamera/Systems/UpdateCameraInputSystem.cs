using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.Emotes;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.Input;
using DCL.Input.Systems;
using DCL.InWorldCamera;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateCameraInputSystem : UpdateInputSystem<CameraInput, CameraComponent>
    {
        private const float CURSOR_DIRTY_THRESHOLD = 1f;
        private const float SPRINT_CAMERA_INPUT_FREE_MOVEMENT_MODIFIER = 10;
        private const float SPRINT_CAMERA_INPUT_FREE_PANNING_MODIFIER = 10;
        private const float SPRINT_CAMERA_INPUT_FREE_FOV_MODIFIER = 2;
        private const float SLOW_CAMERA_INPUT_FREE_MOVEMENT_MODIFIER = 0.5f;
        private const float SLOW_CAMERA_INPUT_FREE_PANNING_MODIFIER = 0.5f;
        private const float SLOW_CAMERA_INPUT_FREE_FOV_MODIFIER = 0.5f;

        private readonly DCLInput.CameraActions cameraActions;
        private readonly DCLInput.FreeCameraActions freeCameraActions;
        private readonly Entity playerEntity;

        internal UpdateCameraInputSystem(World world, Entity playerEntity) : base(world)
        {
            cameraActions = DCLInput.Instance.Camera;
            freeCameraActions = DCLInput.Instance.FreeCamera;
            this.playerEntity = playerEntity;
        }

        protected override void Update(float t)
        {
            UpdateInputQuery(World);
            ResetInputQuery(World);
        }

        [Query]
        [All(typeof(CameraBlockerComponent))]
        private void ResetInput(ref CameraInput cameraInput)
        {
            cameraInput.ZoomIn = false;
            cameraInput.ZoomOut = false;
            cameraInput.Delta = Vector2.zero;
            cameraInput.FreeMovement = Vector2.zero;
            cameraInput.FreePanning = Vector2.zero;
        }

        [Query]
        [None(typeof(CameraBlockerComponent))]
        private void UpdateInput(ref CameraInput cameraInput, ref CursorComponent cursorComponent, CameraComponent cameraComponent)
        {
            if (!cameraActions.enabled)
            {
                cameraInput.ZoomIn = false;
                cameraInput.ZoomOut = false;
                cameraInput.Delta = Vector2.zero;
            }
            else
            {
                if (!World.Get<CharacterEmoteComponent>(playerEntity).IsPlayingEmote /*&&
                    cameraComponent.Mode == CameraMode.ThirdPerson*/) // Changing the camera is not allowed while playing an emote
                {
                    cameraInput.ZoomIn = cameraActions.Zoom.ReadValue<Vector2>().y > 0
                                     || cameraActions.ZoomIn.WasPressedThisFrame();

                    cameraInput.ZoomOut = cameraActions.Zoom.ReadValue<Vector2>().y < 0
                                          || cameraActions.ZoomOut.WasPressedThisFrame();
                }

                Vector2 currentDelta = cameraActions.Delta.ReadValue<Vector2>();

                if (currentDelta.sqrMagnitude > CURSOR_DIRTY_THRESHOLD)
                    cursorComponent.PositionIsDirty = true;

                cameraInput.Delta = cursorComponent.CursorState != CursorState.Free && cursorComponent.CursorState != CursorState.LockedWithUI ? currentDelta : Vector2.zero;
            }

            if (!freeCameraActions.enabled)
            {
                cameraInput.FreeMovement = Vector2.zero;
                cameraInput.FreePanning = Vector2.zero;
            }
            else
            {
                cameraInput.FreeMovement = freeCameraActions.Movement.ReadValue<Vector2>();
                cameraInput.FreePanning = freeCameraActions.Panning.ReadValue<Vector2>();
                cameraInput.FreeFOV = freeCameraActions.FOV.ReadValue<Vector2>();

                if (freeCameraActions.Sprint.IsPressed())
                {
                    cameraInput.FreeMovement *= SPRINT_CAMERA_INPUT_FREE_MOVEMENT_MODIFIER;
                    cameraInput.FreePanning *= SPRINT_CAMERA_INPUT_FREE_PANNING_MODIFIER;
                    cameraInput.FreeFOV *= SPRINT_CAMERA_INPUT_FREE_FOV_MODIFIER;
                }

                if (freeCameraActions.Slow.IsPressed())
                {
                    cameraInput.FreeMovement *= SLOW_CAMERA_INPUT_FREE_MOVEMENT_MODIFIER;
                    cameraInput.FreePanning *= SLOW_CAMERA_INPUT_FREE_PANNING_MODIFIER;
                    cameraInput.FreeFOV *= SLOW_CAMERA_INPUT_FREE_FOV_MODIFIER;
                }
            }
        }
    }
}
