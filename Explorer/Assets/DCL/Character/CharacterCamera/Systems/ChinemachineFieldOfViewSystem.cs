using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using ECS.Abstract;

namespace DCL.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    public partial class ChinemachineFieldOfViewSystem : BaseUnityLoopSystem
    {
        internal ChinemachineFieldOfViewSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ApplyQuery(World);
        }

        [Query]
        private void Apply(
            ref ICinemachinePreset cinemachinePreset,
            ref CameraComponent camera,
            in CameraFieldOfViewComponent fieldOfViewComponent)
        {
            switch (camera.Mode)
            {
                case CameraMode.ThirdPerson:
                    CinemachineFreeLook tpc = cinemachinePreset.ThirdPersonCameraData.Camera;
                    LensSettings tpcMLens = tpc.m_Lens;
                    tpcMLens.FieldOfView = 60 + fieldOfViewComponent.AdditiveFov;
                    tpc.m_Lens = tpcMLens;
                    break;

                case CameraMode.FirstPerson:
                    ICinemachineFirstPersonCameraData firstPersonCamera = cinemachinePreset.FirstPersonCameraData;
                    firstPersonCamera.Camera.m_Lens.FieldOfView = 60 + fieldOfViewComponent.AdditiveFov;
                    break;
            }
        }
    }
}
