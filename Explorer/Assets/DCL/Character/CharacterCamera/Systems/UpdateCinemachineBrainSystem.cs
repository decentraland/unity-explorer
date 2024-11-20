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
    public partial class UpdateCinemachineBrainSystem : BaseUnityLoopSystem
    {
        public UpdateCinemachineBrainSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            ManualBrainUpdateQueryQuery(World);
        }

        [Query]
        [None(typeof(CameraLookAtIntent))]
        private void ManualBrainUpdateQuery(ref ICinemachinePreset cinemachinePreset)
        {
            // We update brain manually in order to handle properly CameraLookAtIntent component
            cinemachinePreset.Brain.ManualUpdate();
        }
    }
}
