using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.Input;
using DCL.Input.Component;
using ECS.Abstract;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Controls switching between First Person and Third Person camera modes.
    ///     When in Third Person mode, it also controls the camera distance from the character.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ControlCinemachineVirtualCameraSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput dclInput;
        private SingleInstanceEntity inputMap;
        private bool wantsToSwitchState;
        private bool wantsToChangeShoulder;
        private int hotkeySwitchStateDirection = 1;

        internal ControlCinemachineVirtualCameraSystem(World world, DCLInput dclInput) : base(world)
        {
            this.dclInput = dclInput;
            dclInput.Camera.SwitchState.performed += OnSwitchState;
            dclInput.Camera.ChangeShoulder.performed += OnChangeShoulder;
        }

        private void OnChangeShoulder(InputAction.CallbackContext obj)
        {
            wantsToChangeShoulder = true;
        }

        private void OnSwitchState(InputAction.CallbackContext obj)
        {
            wantsToSwitchState = true;
        }

        public override void Initialize()
        {
            inputMap = World.CacheInputMap();

            ApplyDefaultCameraModeQuery(World);
        }

        [Query]
        private void ApplyDefaultCameraMode(ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            cinemachinePreset.FreeCameraData.Camera.enabled = false;
            cinemachinePreset.FirstPersonCameraData.Camera.enabled = false;
            cinemachinePreset.ThirdPersonCameraData.Camera.enabled = false;
            cinemachinePreset.DroneViewCameraData.Camera.enabled = false;

            camera.Mode = cinemachinePreset.DefaultCameraMode;
            SetActiveCamera(cinemachinePreset, in camera, ref cameraState);
        }

        protected override void Update(float t)
        {
            HandleZoomingQuery(World);
            HandleSwitchStateQuery(World);
            HandleOffsetQuery(World, t);
        }

        [Query]
        private void HandleSwitchState(ref CameraComponent cameraComponent, ref ICinemachinePreset cinemachinePreset, ref CinemachineCameraState state)
        {
            if (!wantsToSwitchState) return;
            wantsToSwitchState = false;

            if (!HandleModeSwitch(hotkeySwitchStateDirection, ref cameraComponent))
                HandleModeSwitch(hotkeySwitchStateDirection, ref cameraComponent);

            SwitchCamera(cameraComponent.Mode, ref cinemachinePreset, ref cameraComponent, ref state);
        }

        private void SwitchCamera(CameraMode targetCameraMode, ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            if (camera.Mode == targetCameraMode && IsCorrectCameraEnabled(camera.Mode, cinemachinePreset))
                return;

            cameraState.CurrentCamera.enabled = false;
            camera.Mode = targetCameraMode;

            SetActiveCamera(cinemachinePreset, in camera, ref cameraState);
        }

        private static void SetActiveCamera(ref CinemachineCameraState cameraState, CinemachineVirtualCameraBase camera)
        {
            cameraState.CurrentCamera = camera;
            camera.enabled = true;
        }

        private void SetActiveCamera(ICinemachinePreset cinemachinePreset, in CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);

            if (camera.Mode == CameraMode.Free)
            {
                // Disable Player input
                inputMapComponent.Active |= InputMapComponent.Kind.FreeCamera;
                inputMapComponent.Active &= ~InputMapComponent.Kind.Player;
            }
            else
            {
                // Disable FreeCamera input
                inputMapComponent.Active |= InputMapComponent.Kind.Player;
                inputMapComponent.Active &= ~InputMapComponent.Kind.FreeCamera;
            }

            switch (camera.Mode)
            {
                case CameraMode.FirstPerson:
                    SetActiveCamera(ref cameraState, cinemachinePreset.FirstPersonCameraData.Camera);
                    break;
                case CameraMode.ThirdPerson:
                    SetActiveCamera(ref cameraState, cinemachinePreset.ThirdPersonCameraData.Camera);
                    break;
                case CameraMode.DroneView:
                    SetActiveCamera(ref cameraState, cinemachinePreset.DroneViewCameraData.Camera);
                    break;
                case CameraMode.Free:
                    SetActiveCamera(ref cameraState, cinemachinePreset.FreeCameraData.Camera);
                    SetDefaultFreeCameraPosition(in cinemachinePreset);
                    break;
            }
        }

        [Query]
        [None(typeof(CameraBlockerComponent))]
        private void HandleZooming(ref CameraComponent cameraComponent, ref CameraInput input, ref ICinemachinePreset cinemachinePreset, ref CinemachineCameraState state, in CursorComponent cursorComponent)
        {
            if (cursorComponent.IsOverUI)
                return;

            if (input is { ZoomIn: false, ZoomOut: false })
                return;

            int direction = input.ZoomOut ? 1 : -1;
            HandleModeSwitch(direction, ref cameraComponent);
            SwitchCamera(cameraComponent.Mode, ref cinemachinePreset, ref cameraComponent, ref state);
        }

        private bool HandleModeSwitch(int direction, ref CameraComponent cameraComponent)
        {
            var maxReached = false;

            switch (direction)
            {
                case > 0:
                    switch (cameraComponent.Mode)
                    {
                        case CameraMode.DroneView:
                            maxReached = true;
                            break;
                        case CameraMode.ThirdPerson:
                            cameraComponent.Mode = CameraMode.DroneView;
                            return true;
                        case CameraMode.FirstPerson:
                            cameraComponent.Mode = CameraMode.ThirdPerson;
                            return true;
                    }

                    break;
                case < 0:
                    switch (cameraComponent.Mode)
                    {
                        case CameraMode.DroneView:
                            cameraComponent.Mode = CameraMode.ThirdPerson;
                            return true;
                        case CameraMode.ThirdPerson:
                            cameraComponent.Mode = CameraMode.FirstPerson;
                            return true;
                        case CameraMode.FirstPerson:
                            maxReached = true;
                            break;
                    }

                    break;
            }

            if (!maxReached)
                return false;

            hotkeySwitchStateDirection *= -1;
            return false;

        }

        [Query]
        private void HandleOffset([Data] float dt, ref CameraComponent cameraComponent, ref ICinemachinePreset cinemachinePreset)
        {
            if (cameraComponent.Mode == CameraMode.ThirdPerson)
            {
                if (wantsToChangeShoulder)
                {
                    wantsToChangeShoulder = false;

                    cameraComponent.Shoulder = cameraComponent.Shoulder switch
                                               {
                                                   ThirdPersonCameraShoulder.Center => ThirdPersonCameraShoulder.Right,
                                                   ThirdPersonCameraShoulder.Right => ThirdPersonCameraShoulder.Left,
                                                   ThirdPersonCameraShoulder.Left => ThirdPersonCameraShoulder.Center,
                                               };
                }

                ThirdPersonCameraShoulder thirdPersonCameraShoulder = cameraComponent.Shoulder;

                /*if (cursorComponent.CursorState == CursorState.Free)
                {
                    thirdPersonCameraShoulder = ThirdPersonCameraShoulder.Center;
                }*/

                float value = cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.Value;

                Vector3 offset;

                if (value < 0.5f)
                    offset = Vector3.Lerp(cinemachinePreset.ThirdPersonCameraData.OffsetBottom, cinemachinePreset.ThirdPersonCameraData.OffsetMid, value * 2);
                else
                    offset = Vector3.Lerp(cinemachinePreset.ThirdPersonCameraData.OffsetMid, cinemachinePreset.ThirdPersonCameraData.OffsetTop, (value - 0.5f) * 2);

                offset.x *= thirdPersonCameraShoulder switch
                            {
                                ThirdPersonCameraShoulder.Right => 1,
                                ThirdPersonCameraShoulder.Left => -1,
                                ThirdPersonCameraShoulder.Center => 0,
                            };

                cinemachinePreset.ThirdPersonCameraData.CameraOffset.m_Offset = Vector3.MoveTowards(cinemachinePreset.ThirdPersonCameraData.CameraOffset.m_Offset, offset, 10 * dt);
            }

            wantsToChangeShoulder = false;
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

        private bool IsCorrectCameraEnabled(CameraMode mode, ICinemachinePreset cinemachinePreset)
        {
            switch (mode)
            {
                case CameraMode.FirstPerson:
                    return cinemachinePreset.FirstPersonCameraData.Camera.enabled;
                case CameraMode.ThirdPerson:
                    return cinemachinePreset.ThirdPersonCameraData.Camera.enabled;
                case CameraMode.DroneView:
                    return cinemachinePreset.DroneViewCameraData.Camera.enabled;
                default:
                    return cinemachinePreset.FreeCameraData.Camera.enabled;
            }
        }
    }
}
