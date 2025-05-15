using DCL.Rendering.GPUInstancing;
using DCL.Roads.Settings;
using ECS;
using System;

namespace DCL.Roads
{
    public class RoadsPresence : IDisposable
    {
        private readonly GPUInstancingService gpuInstancingService;
        private readonly RealmData realmData;

        private RoadSettingsAsset roadSettingsAsset;

        public RoadsPresence(RealmData realmData, GPUInstancingService gpuInstancingService)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
        }

        public void Initialize(RoadSettingsAsset roadSettingsAsset)
        {
            this.roadSettingsAsset = roadSettingsAsset;
            realmData.RealmType.OnUpdate += SwitchRoadsInstancedRendering;
        }

        public void Dispose()
        {
            realmData.RealmType.OnUpdate -= SwitchRoadsInstancedRendering;
        }

        private void SwitchRoadsInstancedRendering(RealmKind realmKind)
        {
            return;
            if (realmKind == RealmKind.GenesisCity)
                gpuInstancingService.AddToIndirect(roadSettingsAsset.IndirectLODGroups);
            else
                gpuInstancingService.Remove(roadSettingsAsset.IndirectLODGroups);
        }
    }
}
