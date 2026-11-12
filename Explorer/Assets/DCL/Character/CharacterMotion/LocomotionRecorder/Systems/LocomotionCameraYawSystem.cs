using Arch.Core;
using Arch.SystemGroups;
using Cinemachine;
using DCL.Character.CharacterCamera.Systems;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using DCL.Diagnostics;
using ECS.Abstract;

namespace DCL.CharacterMotion.LocomotionRecorder
{
    /// <summary>
    ///     Holds the third-person camera at the scenario's yaw while a run is set up and played: the orbit's heading is
    ///     re-asserted after the production camera input and before the brain update, so mouse look cannot turn it.
    ///     The orbit binds in world space, so its X axis value is the world yaw.
    /// </summary>
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateBefore(typeof(UpdateCinemachineBrainSystem))]
    [LogCategory(ReportCategory.MCP)]
    public partial class LocomotionCameraYawSystem : BaseUnityLoopSystem
    {
        private readonly LocomotionSuiteSession session;

        private SingleInstanceEntity camera;

        internal LocomotionCameraYawSystem(World world, LocomotionSuiteSession session) : base(world)
        {
            this.session = session;
        }

        public override void Initialize()
        {
            camera = World.CacheCamera();
        }

        protected override void Update(float t)
        {
            if (!session.TryGetHeldCameraYaw(out float yaw)
                || camera.GetCameraComponent(World).Mode != CameraMode.ThirdPerson
                || !World.TryGet(camera, out ICinemachinePreset? cinemachinePreset)
                || cinemachinePreset == null)
                return;

            CinemachineFreeLook thirdPerson = cinemachinePreset.ThirdPersonCameraData.Camera;
            thirdPerson.m_XAxis.Value = yaw;
            thirdPerson.m_XAxis.m_InputAxisValue = 0f;
            thirdPerson.m_YAxis.m_InputAxisValue = 0f;
        }
    }
}
