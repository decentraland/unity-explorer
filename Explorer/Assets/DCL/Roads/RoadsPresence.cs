using DCL.Rendering.GPUInstancing;
using DCL.Roads.Settings;
using ECS;
using ECS.SceneLifeCycle.SingleScene;
using System;

namespace DCL.Roads
{
    public class RoadsPresence : IDisposable
    {
        private readonly GPUInstancingService gpuInstancingService;
        private readonly RealmData realmData;
        private readonly SingleSceneMode singleSceneMode;

        private RoadSettingsAsset roadSettingsAsset;

        public RoadsPresence(RealmData realmData, GPUInstancingService gpuInstancingService, SingleSceneMode singleSceneMode)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
            this.singleSceneMode = singleSceneMode;
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
            if (realmKind == RealmKind.GenesisCity && !singleSceneMode.IsActive)
                gpuInstancingService.AddToIndirect(roadSettingsAsset.IndirectLODGroups);
            else
                gpuInstancingService.Remove(roadSettingsAsset.IndirectLODGroups);
        }
    }
}
