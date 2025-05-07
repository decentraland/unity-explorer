using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Systems;
using ECS.Abstract;

namespace DCL.Character.CharacterCamera.Systems
{
    [UpdateInGroup(typeof(CameraGroup))]
    [UpdateAfter(typeof(ApplyCinemachineCameraInputSystem))]
    [UpdateAfter(typeof(ControlCinemachineVirtualCameraSystem))]
    public partial class UpdateCinemachineBrainSystem : BaseUnityLoopSystem
    {
        public UpdateCinemachineBrainSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ManualBrainUpdateQuery(World);
        }

        [Query]
        private void ManualBrainUpdate(ref ICinemachinePreset cinemachinePreset)
        {
            cinemachinePreset.Brain.ManualUpdate();
        }
    }
}
