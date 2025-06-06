using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using ECS.Abstract;
using UnityEngine;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    public partial class CinemachineFarClipPlaneSystem : BaseUnityLoopSystem
    {
        private CinemachineFarClipPlaneSystem(World world) : base(world)
        {
        }

        protected override void Update(float t)
        {
            ApplyQuery(World);
        }

        [Query]
        private void Apply(ref ICinemachinePreset cinemachinePreset, ref CameraComponent camera)
        {
            var settings = cinemachinePreset.FarClipPlaneSettings;

            switch (camera.Mode)
            {
                case CameraMode.ThirdPerson:
                    ICinemachineThirdPersonCameraData thirdPersonCamera = cinemachinePreset.ThirdPersonCameraData;
                    UpdateFarClipPlane(thirdPersonCamera.Camera.Follow, settings, ref thirdPersonCamera.Camera.m_Lens);
                    break;

                case CameraMode.FirstPerson:
                    ICinemachineFirstPersonCameraData firstPersonCamera = cinemachinePreset.FirstPersonCameraData;
                    UpdateFarClipPlane(firstPersonCamera.Camera.Follow, settings, ref firstPersonCamera.Camera.m_Lens);
                    break;

                case CameraMode.DroneView:
                    ICinemachineThirdPersonCameraData droneCamera = cinemachinePreset.DroneViewCameraData;
                    UpdateFarClipPlane(droneCamera.Camera.Follow, settings, ref droneCamera.Camera.m_Lens);
                    break;

                case CameraMode.Free:
                    cinemachinePreset.FreeCameraData.Camera.m_Lens.FarClipPlane = settings.MaxFarClipPlane;
                    break;

                case CameraMode.InWorld:
                    cinemachinePreset.InWorldCameraData.Camera.m_Lens.FarClipPlane = settings.MaxFarClipPlane;
                    break;
            }

        }

        private void UpdateFarClipPlane(Transform target, CameraFarClipPlaneSettings settings, ref LensSettings lens)
        {
            float altitude = target.position.y;

            float farClipPlaneNormalized = Mathf.InverseLerp(settings.MinFarClipPlaneAltitude, settings.MaxFarClipPlaneAltitude, altitude);
            float farClipPlane = Mathf.Lerp(settings.MinFarClipPlane, settings.MaxFarClipPlane, farClipPlaneNormalized);

            lens.FarClipPlane = farClipPlane;
        }
    }
}
