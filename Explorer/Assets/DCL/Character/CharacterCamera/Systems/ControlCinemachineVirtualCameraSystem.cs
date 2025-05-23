﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Audio;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.CharacterCamera.Settings;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.CharacterMotion.Components;
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
        private readonly Transform cameraFocus;
        private readonly CameraMovementPOVSettings povSettings;
        private readonly ICinemachineCameraAudioSettings cinemachineCameraAudioSettings;

        private Transform cameraFocusParent;
        private Vector3 offset;

        private int hotkeySwitchStateDirection = 1;

        internal ControlCinemachineVirtualCameraSystem(World world, Transform cameraFocus, CameraMovementPOVSettings povSettings,ICinemachineCameraAudioSettings cinemachineCameraAudioSettings) : base(world)
        {
            this.cameraFocus = cameraFocus;
            this.povSettings = povSettings;
            this.cinemachineCameraAudioSettings = cinemachineCameraAudioSettings;
        }

        public override void Initialize()
        {
            cameraFocusParent = cameraFocus.transform.parent;
            cameraFocus.transform.parent = null;
            offset = cameraFocus.position - cameraFocusParent.position;

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
            cameraFocus.transform.position = cameraFocusParent.position + offset;

            HandleCameraInputQuery(World, t);
            UpdateCameraStateQuery(World);
            UpdateCameraFocusOnTeleportQuery(World);
        }

        [Query]
        private void UpdateCameraFocusOnTeleport(in PlayerTeleportIntent teleportIntent)
        {
            cameraFocus.transform.position = teleportIntent.Position;
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
            ICinemachineThirdPersonCameraData? cameraData = cameraComponent.Mode switch
                                                            {
                                                                CameraMode.ThirdPerson => cinemachinePreset.ThirdPersonCameraData,
                                                                CameraMode.DroneView => cinemachinePreset.DroneViewCameraData,
                                                                _ => null,
                                                            };

            if (cameraData == null)
                return;

            if (input.ChangeShoulder)
                cameraComponent.Shoulder = cameraComponent.Shoulder switch
                                           {
                                               ThirdPersonCameraShoulder.Right => ThirdPersonCameraShoulder.Left,
                                               ThirdPersonCameraShoulder.Left => ThirdPersonCameraShoulder.Right,
                                           };

            ThirdPersonCameraShoulder thirdPersonCameraShoulder = cameraComponent.Shoulder;

            float currentPitch = cameraFocus.rotation.eulerAngles.x;
            currentPitch = ((currentPitch + 180) % 360) - 180;
            currentPitch = Mathf.Clamp(currentPitch, povSettings.MinVerticalAngle, povSettings.MaxVerticalAngle);

            float normalized = currentPitch <= 0f
                ? 0.5f * Mathf.InverseLerp(povSettings.MinVerticalAngle, 0f, currentPitch) // minPitch → 0,  0 → 0.5
                : 0.5f + (0.5f * Mathf.InverseLerp(0f, povSettings.MaxVerticalAngle, currentPitch)); // 0  → 0.5,  maxPitch → 1

            // first half of the curve (0..0.5) is for bottom-mid rig, second (0.5..1) is for mid-top
            float curveValue = cameraData.DistanceScale.Evaluate(normalized);

            // in order to lerp the offset correctly, the value from the YAxis goes from 0 to 1, being 0.5 the middle rig
            Vector3 offset = curveValue < 0.5f
                ? Vector3.Lerp(cameraData.OffsetBottom, cameraData.OffsetMid, curveValue * 2)
                : Vector3.Lerp(cameraData.OffsetMid, cameraData.OffsetTop, (curveValue - 0.5f) * 2);

            if (cursorComponent.CursorState != CursorState.Locked)
                thirdPersonCameraShoulder = ThirdPersonCameraShoulder.Center;

            float targetSide = thirdPersonCameraShoulder switch
                               {
                                   ThirdPersonCameraShoulder.Right => 1f,
                                   ThirdPersonCameraShoulder.Left => 0f,
                                   ThirdPersonCameraShoulder.Center => 0.5f,
                               };
            cameraData.ThirdPersonFollow.CameraSide = Mathf.MoveTowards(cameraData.ThirdPersonFollow.CameraSide, targetSide, cinemachinePreset.ShoulderChangeSpeed * dt);

            cameraData.ThirdPersonFollow.ShoulderOffset.x = offset.x;
            cameraData.ThirdPersonFollow.VerticalArmLength = offset.y;
            cameraData.ThirdPersonFollow.CameraDistance = offset.z;
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
            if (!IsCorrectCameraEnabled(targetCameraMode, cinemachinePreset))
                ProcessCameraActivation(targetCameraMode, cinemachinePreset, ref camera, ref cameraState);
        }

        private static bool IsCorrectCameraEnabled(CameraMode mode, ICinemachinePreset cinemachinePreset) =>
            mode switch
            {
                CameraMode.FirstPerson => cinemachinePreset.FirstPersonCameraData.Camera.enabled,
                CameraMode.ThirdPerson => cinemachinePreset.ThirdPersonCameraData.Camera.enabled,
                CameraMode.DroneView => cinemachinePreset.DroneViewCameraData.Camera.enabled,
                _ => cinemachinePreset.FreeCameraData.Camera.enabled
            };

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
                    cinemachinePreset.ThirdPersonCameraData.Camera.m_Transitions.m_InheritPosition = true;

                    if (camera.PreviousMode is CameraMode.FirstPerson or CameraMode.SDKCamera)
                    {
                        cinemachinePreset.ThirdPersonCameraData.Camera.m_Transitions.m_InheritPosition = false;

                        float yaw = cinemachinePreset.FirstPersonCameraData.POV.m_HorizontalAxis.Value;
                        float pitch = cinemachinePreset.FirstPersonCameraData.POV.m_VerticalAxis.Value;

                        cameraFocus.rotation = Quaternion.Euler(pitch, yaw, 0f);
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
                    Vector3 euler = cameraFocus.localEulerAngles;
                    cinemachinePreset.FreeCameraData.POV.m_HorizontalAxis.Value = euler.y;
                    cinemachinePreset.FreeCameraData.POV.m_VerticalAxis.Value = euler.x;
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
