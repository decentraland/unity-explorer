using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
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
        private SingleInstanceEntity inputMap;
        private int hotkeySwitchStateDirection = 1;

        internal ControlCinemachineVirtualCameraSystem(World world) : base(world) { }

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
            HandleCameraInputQuery(World, t);
            UpdateCameraStateQuery(World);
        }

        [Query]
        [None(typeof(CameraBlockerComponent))]
        private void HandleCameraInput([Data] float dt, in CameraComponent cameraComponent)
        {
            // this blocks the user of changing the current camera, but the SDK still can do it
            if (!cameraComponent.CameraInputChangeEnabled)
                return;

            HandleZoomingQuery(World);
            HandleSwitchStateQuery(World);
            HandleFreeFlyStateQuery(World);
            HandleOffsetQuery(World, dt);
        }

        [Query]
        private void UpdateCameraState(ref CameraComponent cameraComponent, ref ICinemachinePreset cinemachinePreset, ref CinemachineCameraState state)
        {
            SwitchCamera(cameraComponent.Mode, ref cinemachinePreset, ref cameraComponent, ref state);
        }

        [Query]
        private void HandleZooming(ref CameraComponent cameraComponent, ref CameraInput input, in CursorComponent cursorComponent)
        {
            if (cursorComponent.IsOverUI)
                return;

            if (input is { ZoomIn: false, ZoomOut: false })
                return;

            int direction = input.ZoomOut ? 1 : -1;
            HandleModeSwitch(direction, ref cameraComponent, false);
        }

        [Query]
        private void HandleFreeFlyState(ref CameraComponent cameraComponent, in CameraInput input)
        {
            if (input.SetFreeFly)
                cameraComponent.Mode = cameraComponent.Mode != CameraMode.Free ? CameraMode.Free : CameraMode.ThirdPerson;
        }

        [Query]
        private void HandleSwitchState(ref CameraComponent cameraComponent, in CameraInput input)
        {
            if (!input.SwitchState)
                return;

            if (!HandleModeSwitch(hotkeySwitchStateDirection, ref cameraComponent, true))
                hotkeySwitchStateDirection *= -1;
        }

        private void SwitchCamera(CameraMode targetCameraMode, ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            if (IsCorrectCameraEnabled(camera.Mode, cinemachinePreset))
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

        private bool HandleModeSwitch(int direction, ref CameraComponent cameraComponent, bool pingPong)
        {
            switch (direction)
            {
                case > 0:
                    switch (cameraComponent.Mode)
                    {
                        case CameraMode.DroneView:
                            if (pingPong)
                                cameraComponent.Mode = CameraMode.ThirdPerson;

                            return false;
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
                            if (pingPong)
                                cameraComponent.Mode = CameraMode.ThirdPerson;

                            return false;
                    }

                    break;
            }

            return false;
        }

        [Query]
        private void HandleOffset([Data] float dt, ref CameraComponent cameraComponent, ref ICinemachinePreset cinemachinePreset, in CameraInput input, in CursorComponent cursorComponent)
        {
            if (cameraComponent.Mode is not (CameraMode.DroneView or CameraMode.ThirdPerson))
                return;

            ICinemachineThirdPersonCameraData cameraData = cameraComponent.Mode == CameraMode.ThirdPerson ? cinemachinePreset.ThirdPersonCameraData : cinemachinePreset.DroneViewCameraData;

            if (input.ChangeShoulder)
                cameraComponent.Shoulder = cameraComponent.Shoulder switch
                                           {
                                               ThirdPersonCameraShoulder.Right => ThirdPersonCameraShoulder.Left,
                                               ThirdPersonCameraShoulder.Left => ThirdPersonCameraShoulder.Right,
                                           };

            ThirdPersonCameraShoulder thirdPersonCameraShoulder = cameraComponent.Shoulder;

            float value = cameraData.Camera.m_YAxis.Value;

            Vector3 offset;

            if (value < 0.5f)
                offset = Vector3.Lerp(cameraData.OffsetBottom, cameraData.OffsetMid, value * 2);
            else
                offset = Vector3.Lerp(cameraData.OffsetMid, cameraData.OffsetTop, (value - 0.5f) * 2);

            if (cursorComponent.CursorState != CursorState.Locked)
                thirdPersonCameraShoulder = ThirdPersonCameraShoulder.Center;

            offset.x *= thirdPersonCameraShoulder switch
                        {
                            ThirdPersonCameraShoulder.Right => 1,
                            ThirdPersonCameraShoulder.Left => -1,
                            ThirdPersonCameraShoulder.Center => 0,
                        };

            cameraData.CameraOffset.m_Offset = Vector3.MoveTowards(cameraData.CameraOffset.m_Offset, offset, cinemachinePreset.ShoulderChangeSpeed * dt);
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
