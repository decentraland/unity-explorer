using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera.Components;
using DCL.Diagnostics;
using DCL.InWorldCamera;
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
        private readonly bool isFreeCameraAllowed;
        private readonly Transform cameraFocusParent;
        private readonly Vector3 offset;

        internal ApplyCinemachineCameraInputSystem(World world, DCLInput input, Transform cameraFocus, bool isFreeCameraAllowed) : base(world)
        {
            this.input = input;
            this.cameraFocus = cameraFocus;
            this.isFreeCameraAllowed = isFreeCameraAllowed;

            cameraFocusParent = cameraFocus.transform.parent;
            cameraFocus.transform.parent = null;

            offset = cameraFocus.position - cameraFocusParent.position;
        }

        protected override void Update(float t)
        {
            ApplyQuery(World!, t);
            ForceLookAtQuery(World!);

            cameraFocus.transform.position = cameraFocusParent.position + offset;
        }

        [Query]
        [None(typeof(CameraLookAtIntent), typeof(InWorldCameraComponent))]
        private void Apply([Data] float dt, ref CameraComponent camera, ref CameraInput cameraInput, ref ICinemachinePreset cinemachinePreset)
        {
            switch (camera.Mode)
            {
                case CameraMode.DroneView:
                    CinemachineFreeLook dvc = cinemachinePreset.DroneViewCameraData.Camera;
                    dvc.m_XAxis.m_InputAxisValue = cameraInput.Delta.x;
                    dvc.m_YAxis.m_InputAxisValue = cameraInput.Delta.y;
                    break;
                case CameraMode.ThirdPerson:
                case CameraMode.SDKCamera:
                    float horizontalRotation = cameraInput.Delta.x * 3 * dt;
                    float verticalRotation = cameraInput.Delta.y * 3 * dt;

                    cameraFocus.Rotate(Vector3.up, horizontalRotation, Space.World);

                    float newVerticalAngle = cameraFocus.eulerAngles.x - verticalRotation;
                    if (newVerticalAngle > 180f) newVerticalAngle -= 360f;

                    cameraFocus.localRotation = Quaternion.Euler(newVerticalAngle, cameraFocus.eulerAngles.y, cameraFocus.eulerAngles.z);

                    ApplyPOV(cinemachinePreset.ThirdPersonCameraData.POV, in cameraInput);
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
                    cinemachinePreset.ForceThirdPersonCameraLookAt(lookAtIntent);
                    break;
                case CameraMode.DroneView:
                    cinemachinePreset.ForceDroneCameraLookAt(lookAtIntent);
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
