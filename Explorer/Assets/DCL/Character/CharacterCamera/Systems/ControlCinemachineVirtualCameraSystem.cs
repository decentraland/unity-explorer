﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Audio;
using DCL.Character.CharacterCamera.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.Input;
using DCL.Input.Component;
using DCL.Input.Systems;
using DCL.InWorldCamera;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Character.CharacterCamera.Systems
{
    /// <summary>
    ///     Controls switching between First Person and Third Person camera modes.
    ///     When in Third Person mode, it also controls the camera distance from the character.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    public partial class ControlCinemachineVirtualCameraSystem : BaseUnityLoopSystem
    {
        private SingleInstanceEntity inputMap;
        private readonly ICinemachineCameraAudioSettings cinemachineCameraAudioSettings;
        private int hotkeySwitchStateDirection = 1;

        internal ControlCinemachineVirtualCameraSystem(World world, ICinemachineCameraAudioSettings cinemachineCameraAudioSettings) : base(world)
        {
            this.cinemachineCameraAudioSettings = cinemachineCameraAudioSettings;
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
            cameraState.CurrentCamera = cinemachinePreset.ThirdPersonCameraData.Camera;
            ProcessCameraActivation(cinemachinePreset.DefaultCameraMode, cinemachinePreset, ref camera, ref cameraState);
        }

        protected override void Update(float t)
        {
            HandleCameraInputQuery(World, t);
            UpdateCameraStateQuery(World);
        }

        [Query]
        [None(typeof(CameraBlockerComponent), typeof(InWorldCameraComponent))]
        private void HandleCameraInput([Data] float dt, in CameraComponent cameraComponent)
        {
            if (cameraComponent.Mode == CameraMode.SDKCamera) return;

            // this blocks the user of changing the current camera, but the SDK still can do it
            if (!cameraComponent.CameraInputChangeEnabled) return;

            HandleZoomingQuery(World);
            HandleSwitchStateQuery(World);
            HandleFreeFlyStateQuery(World);
            HandleOffsetQuery(World, dt);
        }

        [Query]
        [None(typeof(InWorldCameraComponent))]
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
        [None(typeof(InWorldCameraComponent))]
        private void HandleSwitchState(ref CameraComponent cameraComponent, in CameraInput input)
        {
            if (!input.SwitchState)
                return;

            if (!HandleModeSwitch(hotkeySwitchStateDirection, ref cameraComponent, true))
                hotkeySwitchStateDirection *= -1;
        }

        [Query]
        [None(typeof(InWorldCameraComponent))]
        private void HandleFreeFlyState(ref CameraComponent cameraComponent, in CameraInput input)
        {
            if (input.SetFreeFly)
                cameraComponent.Mode = cameraComponent.Mode != CameraMode.Free ? CameraMode.Free : CameraMode.ThirdPerson;
        }

        [Query]
        [None(typeof(InWorldCameraComponent))]
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

            // in order to lerp the offset correctly, the value from the YAxis goes from 0 to 1, being 0.5 the middle rig
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

            cameraData.CameraOffset.offset = Vector3.MoveTowards(cameraData.CameraOffset.offset, offset, cinemachinePreset.ShoulderChangeSpeed * dt);
        }

        [Query]
        [None(typeof(InWorldCameraComponent))]
        private void UpdateCameraState(ref CameraComponent cameraComponent, ref ICinemachinePreset cinemachinePreset, ref CinemachineCameraState state)
        {
            if (cameraComponent.Mode == CameraMode.SDKCamera) return;

            SwitchCamera(cameraComponent.Mode, ref cinemachinePreset, ref cameraComponent, ref state);
        }

        private void SwitchCamera(CameraMode targetCameraMode, ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            if (camera.PreviousMode != CameraMode.SDKCamera && IsCorrectCameraEnabled(targetCameraMode, cinemachinePreset))
                return;

            ProcessCameraActivation(targetCameraMode, cinemachinePreset, ref camera, ref cameraState);
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

        private void ProcessCameraActivation(CameraMode targetCameraMode, ICinemachinePreset cinemachinePreset, ref CameraComponent camera, ref CinemachineCameraState cameraState)
        {
            cameraState.CurrentCamera.enabled = false;

            if (targetCameraMode != camera.PreviousMode)
                HandleInputBlock(targetCameraMode, camera.PreviousMode);

            switch (targetCameraMode)
            {
                case CameraMode.FirstPerson:
                    cinemachinePreset.FirstPersonCameraData.Camera.m_Transitions.m_InheritPosition = camera.PreviousMode == CameraMode.ThirdPerson;

                    SetActiveCamera(ref cameraState, cinemachinePreset.FirstPersonCameraData.Camera);
                    break;
                case CameraMode.ThirdPerson:
                    cinemachinePreset.ThirdPersonCameraData.Camera.m_Transitions.m_InheritPosition = camera.PreviousMode != CameraMode.FirstPerson && camera.PreviousMode != CameraMode.SDKCamera;
                    if (camera.PreviousMode == CameraMode.FirstPerson)
                    {
                        cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.Value = cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.Value;

                        // m_VerticalAxis goes from -90 to 90, so we convert that to a 0 to 1 value
                        cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.Value = (90 + cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.Value) / 180f;
                    }

                    SetActiveCamera(ref cameraState, cinemachinePreset.ThirdPersonCameraData.Camera);
                    break;
                case CameraMode.DroneView:
                    cinemachinePreset.DroneViewCameraData.Camera.m_Transitions.m_InheritPosition = camera.PreviousMode == CameraMode.ThirdPerson;

                    SetActiveCamera(ref cameraState, cinemachinePreset.DroneViewCameraData.Camera);
                    break;
                case CameraMode.Free:
                    SetActiveCamera(ref cameraState, cinemachinePreset.FreeCameraData.Camera);

                    // take previous position from third person camera
                    Vector3 tpPos = cinemachinePreset.ThirdPersonCameraData.Camera.transform.position;
                    cinemachinePreset.FreeCameraData.Camera.transform.position = tpPos + cinemachinePreset.FreeCameraData.DefaultPosition;

                    // copy POV
                    cinemachinePreset.FreeCameraData.POV.m_HorizontalAxis.Value = cinemachinePreset.ThirdPersonCameraData.Camera.m_XAxis.Value;
                    cinemachinePreset.FreeCameraData.POV.m_VerticalAxis.Value = cinemachinePreset.ThirdPersonCameraData.Camera.m_YAxis.Value;
                    break;
            }

            camera.Mode = targetCameraMode;

            return;

            void SetActiveCamera(ref CinemachineCameraState cameraState, CinemachineVirtualCameraBase camera)
            {
                cameraState.CurrentCamera = camera;
                camera.enabled = true;
            }
        }

        private void HandleInputBlock(CameraMode targetCameraMode, CameraMode currentCameraMode)
        {
            if (targetCameraMode == CameraMode.Free)
            {
                ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
                inputMapComponent.UnblockInput(InputMapComponent.Kind.FREE_CAMERA);
                inputMapComponent.BlockInput(InputMapComponent.Kind.PLAYER);
            }
            else if (currentCameraMode == CameraMode.Free)
            {
                ref InputMapComponent inputMapComponent = ref inputMap.GetInputMapComponent(World);
                inputMapComponent.UnblockInput(InputMapComponent.Kind.PLAYER);
                inputMapComponent.BlockInput(InputMapComponent.Kind.FREE_CAMERA);
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
                            {
                                UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomInAudio);
                                cameraComponent.Mode = CameraMode.ThirdPerson;
                            }

                            return false;
                        case CameraMode.ThirdPerson:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomOutAudio);
                            cameraComponent.Mode = CameraMode.DroneView;
                            return true;
                        case CameraMode.FirstPerson:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomOutAudio);
                            cameraComponent.Mode = CameraMode.ThirdPerson;
                            return true;
                    }

                    break;
                case < 0:
                    switch (cameraComponent.Mode)
                    {
                        case CameraMode.DroneView:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomInAudio);
                            cameraComponent.Mode = CameraMode.ThirdPerson;
                            return true;
                        case CameraMode.ThirdPerson:
                            UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomInAudio);
                            cameraComponent.Mode = CameraMode.FirstPerson;
                            return true;
                        case CameraMode.FirstPerson:
                            if (pingPong)
                            {
                                UIAudioEventsBus.Instance.SendPlayAudioEvent(cinemachineCameraAudioSettings.ZoomOutAudio);
                                cameraComponent.Mode = CameraMode.ThirdPerson;
                            }

                            return false;
                    }

                    break;
            }

            return false;
        }
    }
}
