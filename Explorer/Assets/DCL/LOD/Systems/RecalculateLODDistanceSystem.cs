using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Metadata;
using DCL.CharacterCamera;
using DCL.LOD.Components;
using ECS.Abstract;
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.SceneDefinition;
using UnityEngine;

namespace DCL.LOD.Systems
{
    [UpdateInGroup(typeof(RealmGroup))]
    public partial class RecalculateLODDistanceSystem : BaseUnityLoopSystem
    {
        public IRealmPartitionSettings realmPartitionSettingsAsset;
        private float defaultFOV;
        private float defaultLodBias;

        public RecalculateLODDistanceSystem(World world, IRealmPartitionSettings realmPartitionSettingsAsset) : base(world)
        {
            this.realmPartitionSettingsAsset = realmPartitionSettingsAsset;
            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged += RecalculateLODDistance;
        }

        public override void Initialize()
        {
            defaultFOV = World.CacheCamera().GetCameraComponent(World).Camera.fieldOfView;
            defaultLodBias = QualitySettings.lodBias;
        }

        public override void Dispose()
        {
            base.Dispose();
            realmPartitionSettingsAsset.OnMaxLoadingDistanceInParcelsChanged -= RecalculateLODDistance;
        }

        private void RecalculateLODDistance(int obj)
        {
            RecalculateSceneLODDistanceQuery(World);
        }

        [Query]
        public void RecalculateSceneLODDistance(ref SceneLODInfo sceneLODInfo, in SceneDefinitionComponent sceneDefinition)
        {
            // Not yet initialized
            if (string.IsNullOrEmpty(sceneLODInfo.id))
                return;

            if (SceneLODInfoUtils.LODCount(sceneLODInfo.metadata.SuccessfullLODs) > 0)
                sceneLODInfo.RecalculateLODDistances(defaultFOV, defaultLodBias, realmPartitionSettingsAsset.MaxLoadingDistanceInParcels, sceneDefinition.Parcels.Count);
        }


        protected override void Update(float t)
        {
        }
    }
}
