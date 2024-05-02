using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera.Components;
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

        internal ApplyCinemachineCameraInputSystem(World world, DCLInput input) : base(world)
        {
            this.input = input;
        }

        protected override void Update(float t)
        {
            ApplyQuery(World, t);
            ForceLookAtQuery(World);
        }

        [Query]
        [None(typeof(CameraLookAtIntent))]
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
                    CinemachineFreeLook tpc = cinemachinePreset.ThirdPersonCameraData.Camera;
                    tpc.m_XAxis.m_InputAxisValue = cameraInput.Delta.x;
                    tpc.m_YAxis.m_InputAxisValue = cameraInput.Delta.y;
                    break;

                case CameraMode.FirstPerson:
                    ApplyPOV(cinemachinePreset.FirstPersonCameraData.POV, in cameraInput);
                    break;
                case CameraMode.Free:
                    ApplyPOV(cinemachinePreset.FreeCameraData.POV, in cameraInput);

                    // Apply free movement
                    ApplyFreeCameraMovement(dt, camera, cameraInput, cinemachinePreset);

                    // Apply Field of View
                    ApplyFOV(dt, cinemachinePreset, in cameraInput);
                    break;
            }

            cameraInput.SetFreeFly = input.Camera.ToggleFreeFly.WasPressedThisFrame();
            cameraInput.SwitchState = input.Camera.SwitchState.WasPressedThisFrame();
            cameraInput.ChangeShoulder = input.Camera.ChangeShoulder.WasPressedThisFrame();

            // Update the brain manually
            cinemachinePreset.Brain.ManualUpdate();
        }

        [Query]
        private void ForceLookAt(in Entity entity, ref CameraComponent camera, ref ICinemachinePreset cinemachinePreset, in CameraLookAtIntent lookAtIntent)
        {
            switch (camera.Mode)
            {
                case CameraMode.DroneView:
                    cinemachinePreset.ForceThirdPersonCameraLookAt(lookAtIntent);
                    break;
                case CameraMode.ThirdPerson:
                    cinemachinePreset.ForceThirdPersonCameraLookAt(lookAtIntent);
                    break;
                case CameraMode.FirstPerson:
                    cinemachinePreset.ForceFirstPersonCameraLookAt(lookAtIntent);
                    break;
                case CameraMode.Free:
                    cinemachinePreset.ForceFreeCameraLookAt(lookAtIntent);
                    break;
            }

            World.Remove<CameraLookAtIntent>(entity);
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
                                                    * cinemachinePreset.FreeCameraData.Speed * dt;


        }

        private void ApplyPOV(CinemachinePOV cinemachinePOV, in CameraInput cameraInput)
        {
            if (cinemachinePOV)
            {
                cinemachinePOV.m_HorizontalAxis.m_InputAxisValue = cameraInput.Delta.x;
                cinemachinePOV.m_VerticalAxis.m_InputAxisValue = cameraInput.Delta.y;
            }
        }

        private void ApplyFOV(float dt, ICinemachinePreset cinemachinePreset, in CameraInput cameraInput)
        {
            CinemachineVirtualCamera tpc = cinemachinePreset.FreeCameraData.Camera;
            LensSettings tpcMLens = tpc.m_Lens;
            tpcMLens.FieldOfView += cameraInput.FreeFOV.y * cinemachinePreset.FreeCameraData.Speed * dt;
            tpc.m_Lens = tpcMLens;
        }
    }
}
