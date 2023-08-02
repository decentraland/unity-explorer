using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Cinemachine;
using DCL.CharacterCamera.Components;
using ECS.Abstract;

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
        internal ApplyCinemachineCameraInputSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ApplyQuery(World);
        }

        [Query]
        private void Apply(ref CameraComponent camera, ref CameraInput cameraInput, ref ICinemachinePreset cinemachinePreset)
        {
            switch (camera.Mode)
            {
                case CameraMode.ThirdPerson:
                {
                    CinemachineFreeLook tpc = cinemachinePreset.ThirdPersonCameraData.Camera;
                    tpc.m_XAxis.Value = cameraInput.Axes.x;
                    tpc.m_YAxis.Value = cameraInput.Axes.y;
                    break;
                }
                case CameraMode.Free:
                {
                    // TODO
                    break;
                }
            }

            // Update the brain manually
            cinemachinePreset.Brain.ManualUpdate();
        }
    }
}
