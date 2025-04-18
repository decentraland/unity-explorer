using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera;
using DCL.Character.CharacterCamera.Components;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera;
using DCL.Settings.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    /// <summary>
    ///     Apply camera's movement after all other calculations.
    ///     Camera movement makes sense for orbit camera only, not for First Person
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    public partial class ApplyCinemachineCameraInputSystem : BaseUnityLoopSystem
    {
        private readonly DCLInput input;
        private readonly Transform cameraFocus;
        private readonly ControlsSettingsAsset settings;
        private readonly bool isFreeCameraAllowed;
        private readonly Transform cameraFocusParent;
        private readonly Vector3 offset;

        internal ApplyCinemachineCameraInputSystem(World world, DCLInput input, Transform cameraFocus, ControlsSettingsAsset settings, bool isFreeCameraAllowed) : base(world)
        {
            this.input = input;
            this.cameraFocus = cameraFocus;
            this.settings = settings;
            this.isFreeCameraAllowed = isFreeCameraAllowed;
        }

        protected override void Update(float t)
        {
            ApplyQuery(World!, t);
            ForceLookAtQuery(World!);
        }

        [Query]
        [None(typeof(CameraLookAtIntent), typeof(InWorldCameraComponent))]
        private void Apply([Data] float dt, ref CameraComponent camera, ref CameraInput cameraInput, ref CameraDampedPOV dampedPOV, ref ICinemachinePreset cinemachinePreset)
        {
            switch (camera.Mode)
            {
                case CameraMode.DroneView:
                case CameraMode.ThirdPerson:
                case CameraMode.SDKCamera:
                    float horizontalRotation = cameraInput.Delta.x * settings.HorizontalMouseSensitivity;
                    float verticalRotation = cameraInput.Delta.y * settings.VerticalMouseSensitivity;
                    Vector2 deltaInput = new Vector2(horizontalRotation, verticalRotation);

                    CameraMovementUtils.Rotate(ref dampedPOV, cameraFocus, deltaInput, settings.CameraMovementPOVSettings, dt);
                    break;
                case CameraMode.FirstPerson:
                    ApplyPOV(cinemachinePreset.FirstPersonCameraData.POV, in cameraInput);
                    break;
                case CameraMode.Free:
                    ApplyPOV(cinemachinePreset.FreeCameraData.POV, in cameraInput);
                    ApplyFreeCameraMovement(dt, camera, cameraInput, cinemachinePreset); // Apply free movement
                    ApplyFOV(dt, cinemachinePreset, in cameraInput); // Apply Field of View
                    break;
                default:
                    ReportHub.LogError(GetReportData(), $"Camera mode is unknown {camera.Mode}");
                    break;
            }

            cameraInput.SetFreeFly = isFreeCameraAllowed && input.Camera.ToggleFreeFly!.triggered;
            cameraInput.SwitchState = input.Camera.SwitchState!.WasPressedThisFrame();
            cameraInput.ChangeShoulder = input.Camera.ChangeShoulder!.WasPressedThisFrame();
        }

        [Query]
        [None(typeof(InWorldCameraComponent))]
        private void ForceLookAt(in Entity entity, in CameraComponent camera, ref ICinemachinePreset cinemachinePreset, in CameraLookAtIntent lookAtIntent)
        {
            // Only process the LookAtIntent if we're not in SDKCamera mode
            if (camera.Mode == CameraMode.SDKCamera) return;

            switch (camera.Mode)
            {
                case CameraMode.FirstPerson:
                    cinemachinePreset.ForceFirstPersonCameraLookAt(lookAtIntent);
                    break;
                case CameraMode.Free:
                case CameraMode.ThirdPerson:
                case CameraMode.DroneView:
                    cameraFocus.transform.rotation = Quaternion.Euler(CinemachineExtensions.GetHorizontalAndVerticalAxisForIntent(lookAtIntent));
                    break;
                default:
                    ReportHub.LogError(GetReportData(), $"Camera mode is unknown {camera.Mode}");
                    break;
            }

            World!.Remove<CameraLookAtIntent>(entity);
        }

        private static void ApplyFreeCameraMovement(float dt, in CameraComponent camera, in CameraInput cameraInput,
            ICinemachinePreset cinemachinePreset)
        {
            // Camera's position is under Cinemachine control
            Transform cinemachineTransform = cinemachinePreset.FreeCameraData.Camera.transform;

            // Camera's rotation is not
            Transform cameraTransform = camera.Camera.transform;

            cinemachineTransform.localPosition += ((cameraTransform.forward * cameraInput.FreeMovement.y) +
                                                   (cameraTransform.up * cameraInput.FreePanning.y) +
                                                   (cameraTransform.right * cameraInput.FreeMovement.x))
                                                  * (cinemachinePreset.FreeCameraData.Speed * dt);
        }

        private static void ApplyPOV(CinemachinePOV cinemachinePOV, in CameraInput cameraInput)
        {
            if (cinemachinePOV)
            {
                cinemachinePOV.m_HorizontalAxis.m_InputAxisValue = cameraInput.Delta.x;
                cinemachinePOV.m_VerticalAxis.m_InputAxisValue = cameraInput.Delta.y;
            }
        }

        private static void ApplyFOV(float dt, ICinemachinePreset cinemachinePreset, in CameraInput cameraInput)
        {
            CinemachineVirtualCamera tpc = cinemachinePreset.FreeCameraData.Camera;
            LensSettings tpcMLens = tpc.m_Lens;
            tpcMLens.FieldOfView += cameraInput.FreeFOV.y * cinemachinePreset.FreeCameraData.Speed * dt;
            tpc.m_Lens = tpcMLens;
        }
    }
}
