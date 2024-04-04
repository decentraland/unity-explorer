using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Component;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Controls switching between First Person and Third Person camera modes.
    ///     When in Third Person mode, it also controls the camera distance from the character.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ControlCinemachineVirtualCameraSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity inputMap;

        internal ControlCinemachineVirtualCameraSystem(World world) : base(world) { }

        public override void Initialize()
        {
            inputMap = World.CacheInputMap();

            // Resolve default state
            ApplyDefaultCameraModeQuery(World);
        }

        [Query]
        private void ApplyDefaultCameraMode(ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            cinemachinePreset.FreeCameraData.Camera.enabled = false;
            cinemachinePreset.FirstPersonCameraData.Camera.enabled = false;
            cinemachinePreset.ThirdPersonCameraData.Camera.enabled = false;

            camera.Mode = cinemachinePreset.DefaultCameraMode;
            SetActiveCamera(cinemachinePreset, in camera, ref cameraState);
        }

        protected override void Update(float t)
        {
            HandleZoomingQuery(World);
        }

        private static void SetActiveCamera(ref CinemachineCameraState cameraState, CinemachineVirtualCameraBase camera)
        {
            cameraState.CurrentCamera = camera;
            camera.enabled = true;
        }

        private void SwitchCamera(CameraMode targetCameraMode, ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            if (camera.Mode == targetCameraMode && IsCorrectCameraEnabled(camera.Mode, cinemachinePreset))
                return;

            cameraState.CurrentCamera.enabled = false;
            camera.Mode = targetCameraMode;

            SetActiveCamera(cinemachinePreset, in camera, ref cameraState);
        }

        private void SetActiveCamera(ICinemachinePreset cinemachinePreset, in CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);

            switch (camera.Mode)
            {
                case CameraMode.FirstPerson:
                    SetActiveCamera(ref cameraState, cinemachinePreset.FirstPersonCameraData.Camera);

                    inputMapComponent.Active |= InputMapComponent.Kind.Player;

                    // Disable FreeCamera input
                    inputMapComponent.Active &= ~InputMapComponent.Kind.FreeCamera;

                    break;
                case CameraMode.ThirdPerson:
                    SetActiveCamera(ref cameraState, cinemachinePreset.ThirdPersonCameraData.Camera);

                    inputMapComponent.Active |= InputMapComponent.Kind.Player;

                    // Disable FreeCamera input
                    inputMapComponent.Active &= ~InputMapComponent.Kind.FreeCamera;

                    break;
                case CameraMode.Free:
                    SetActiveCamera(ref cameraState, cinemachinePreset.FreeCameraData.Camera);

                    inputMapComponent.Active |= InputMapComponent.Kind.FreeCamera;

                    // Disable Player input
                    inputMapComponent.Active &= ~InputMapComponent.Kind.Player;

                    SetDefaultFreeCameraPosition(in cinemachinePreset);

                    break;
            }
        }

        [Query]
        [None(typeof(CameraBlockerComponent))]
        private void HandleZooming(ref CameraComponent cameraComponent, ref CameraInput input, ref ICinemachinePreset cinemachinePreset, ref CinemachineCameraState state, in CursorComponent cursorComponent)
        {
            if (cursorComponent.IsOverUI) return;
            if (cameraComponent.CameraInputChangeEnabled)
            {
                if (input.ZoomOut)
                {
                    switch (cameraComponent.Mode)
                    {
                        // if we switch from FP to TP just zoom at 0 position
                        case CameraMode.FirstPerson:
                            SwitchCamera(CameraMode.ThirdPerson, ref cinemachinePreset, ref cameraComponent, ref state);

                            // Reset zoom value
                            state.ThirdPersonZoomValue = 0f;

                            // Set a freshly assigned value
                            SetThirdPersonZoom(state.ThirdPersonZoomValue, in cinemachinePreset);
                            return;
                        case CameraMode.ThirdPerson:
                            // Zoom out according to sensitivity
                            if (TrySwitchToAnotherMode(ref state.ThirdPersonZoomValue, 1, cinemachinePreset.ThirdPersonCameraData.ZoomSensitivity))
                            {
                                SwitchCamera(CameraMode.Free, ref cinemachinePreset, ref cameraComponent, ref state);
                                SetDefaultFreeCameraPosition(in cinemachinePreset);
                                return;
                            }

                            // Set a freshly assigned value
                            SetThirdPersonZoom(state.ThirdPersonZoomValue, in cinemachinePreset);
                            return;
                    }
                }
                else if (input.ZoomIn)
                {
                    switch (cameraComponent.Mode)
                    {
                        case CameraMode.ThirdPerson:
                            // If we exceed the zoom more than by twice the previous value, switch to FP
                            if (TrySwitchToAnotherMode(ref state.ThirdPersonZoomValue, -1, cinemachinePreset.ThirdPersonCameraData.ZoomSensitivity))
                            {
                                SwitchCamera(CameraMode.FirstPerson, ref cinemachinePreset, ref cameraComponent, ref state);
                                return;
                            }

                            // Set a freshly assigned value
                            SetThirdPersonZoom(state.ThirdPersonZoomValue, in cinemachinePreset);
                            return;
                        case CameraMode.Free:
                            // Switch to third-person
                            SwitchCamera(CameraMode.ThirdPerson, ref cinemachinePreset, ref cameraComponent, ref state);

                            // Reset zoom value to the maximum
                            state.ThirdPersonZoomValue = 1f;
                            return;
                    }
                }
            }

            if (!IsCorrectCameraEnabled(cameraComponent.Mode, cinemachinePreset))
                SwitchCamera(cameraComponent.Mode, ref cinemachinePreset, ref cameraComponent, ref state);
        }

        private static void SetDefaultFreeCameraPosition(in ICinemachinePreset preset)
        {
            // take previous position from third person camera
            Vector3 tpPos = preset.ThirdPersonCameraData.Camera.transform.position;
            preset.FreeCameraData.Camera.transform.position = tpPos + preset.FreeCameraData.DefaultPosition;

            // copy POV
            preset.FreeCameraData.POV.m_HorizontalAxis.Value = preset.ThirdPersonCameraData.Camera.m_XAxis.Value;
            preset.FreeCameraData.POV.m_VerticalAxis.Value = preset.ThirdPersonCameraData.Camera.m_YAxis.Value;
        }

        /// <summary>
        ///     Apply zoom and check if scrolling was enough to switch to another mode
        /// </summary>
        /// <returns></returns>
        private static bool TrySwitchToAnotherMode(ref float zoomValue, int direction, float zoomSensitivity)
        {
            float previousZoomValue = zoomValue;
            float targetUnclampedValue = zoomValue + (zoomSensitivity * direction);

            if (direction < 0)
            {
                if (targetUnclampedValue < 0 && -targetUnclampedValue > previousZoomValue)
                    return true;
            }
            else
            {
                if (targetUnclampedValue > 1 && targetUnclampedValue - previousZoomValue > zoomSensitivity / 2f)
                    return true;
            }

            zoomValue = Mathf.Clamp01(targetUnclampedValue);
            return false;
        }

        private static void SetThirdPersonZoom(float normValue, in ICinemachinePreset cinemachinePreset)
        {
            IReadOnlyList<CinemachineFreeLook.Orbit> zoomInOrbitThreshold = cinemachinePreset.ThirdPersonCameraData.ZoomInOrbitThreshold;
            IReadOnlyList<CinemachineFreeLook.Orbit> zoomOutOrbitThreshold = cinemachinePreset.ThirdPersonCameraData.ZoomOutOrbitThreshold;

            static CinemachineFreeLook.Orbit LerpOrbit(CinemachineFreeLook.Orbit a, CinemachineFreeLook.Orbit b, float t) =>
                new ()
                {
                    m_Height = Mathf.Lerp(a.m_Height, b.m_Height, t),
                    m_Radius = Mathf.Lerp(a.m_Radius, b.m_Radius, t),
                };

            for (var i = 0; i < 3; i++)
            {
                // Lerp orbit values
                // 0 is closest
                // 1 is farthest

                CinemachineFreeLook.Orbit orbitValue = LerpOrbit(zoomInOrbitThreshold[i], zoomOutOrbitThreshold[i], normValue);
                cinemachinePreset.ThirdPersonCameraData.Camera.m_Orbits[i] = orbitValue;
            }
        }

        private bool IsCorrectCameraEnabled(CameraMode mode, ICinemachinePreset cinemachinePreset)
        {
            switch (mode)
            {
                case CameraMode.FirstPerson:
                    return cinemachinePreset.FirstPersonCameraData.Camera.enabled;
                case CameraMode.ThirdPerson:
                    return cinemachinePreset.ThirdPersonCameraData.Camera.enabled;
                default:
                    return cinemachinePreset.FreeCameraData.Camera.enabled;
            }
        }
    }
}
