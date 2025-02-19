﻿using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.DebugUtilities;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Roads.Settings;
using DCL.Roads.Systems;
using ECS;
using Global;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DCL.Optimization.Pools;
using DCL.Roads.GPUInstancing;
using DCL.Roads.GPUInstancing.Playground;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.LOD.Systems
{
    /// <summary>
    ///     LOD Container unites LOD and Road Plugins and their common dependencies
    /// </summary>
    public class LODContainer : DCLWorldContainer<LODContainer.LODContainerSettings>
    {
        private const int LOD_LEVELS = 2;
        private const int LODGROUP_POOL_PREWARM_VALUE = 500;

        private readonly IAssetsProvisioner assetsProvisioner;

        private ProvidedAsset<RoadSettingsAsset> roadSettingsAsset;
        private List<GameObject> roadAssetsPrefabList;
        private ProvidedAsset<LODSettingsAsset> lodSettingsAsset;
        private GPUInstancingService gpuInstancingService;
        private RealmData realmData;

        public LODPlugin LODPlugin { get; private set; } = null!;

        public RoadPlugin RoadPlugin { get; private set; } = null!;

        public RoadAssetsPool RoadAssetsPool { get; private set; } = null!;

        public ILODCache LodCache { get; private set; } = null!;

        private LODContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public static async UniTask<(LODContainer? container, bool success)> CreateAsync(IAssetsProvisioner assetsProvisioner,
            IDecentralandUrlsSource decentralandUrlsSource,
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            RealmData realmData,
            TextureArrayContainerFactory textureArrayContainerFactory,
            IDebugContainerBuilder debugBuilder,
            bool lodEnabled,
            GPUInstancingService gpuInstancingService,
            CancellationToken ct)
        {
            var container = new LODContainer(assetsProvisioner);
            container.gpuInstancingService = gpuInstancingService;
            container.realmData = realmData;

            return await container.InitializeContainerAsync<LODContainer, LODContainerSettings>(settingsContainer, ct, c =>
            {
                var roadDataDictionary = new Dictionary<Vector2Int, RoadDescription>();

                foreach (RoadDescription roadDescription in c.roadSettingsAsset.Value.RoadDescriptions)
                    roadDataDictionary.Add(roadDescription.RoadCoordinate, roadDescription);

                var visualSceneStateResolver = new VisualSceneStateResolver(roadDataDictionary.Keys.ToHashSet());

                var roadAssetPool = new RoadAssetsPool(realmData, c.roadAssetsPrefabList, staticContainer.ComponentsContainer.ComponentPoolsRegistry);
                container.RoadAssetsPool = roadAssetPool;
                staticContainer.CacheCleaner.Register(roadAssetPool);

                // Create plugins
                c.RoadPlugin = new RoadPlugin(staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    roadDataDictionary,
                    staticContainer.ScenesCache,
                    staticContainer.SceneReadinessReportQueue,
                    roadAssetPool);

                IComponentPool<LODGroup> lodGroupPool = staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddGameObjectPool(LODGroupPoolUtils.CreateLODGroup, onRelease: LODGroupPoolUtils.ReleaseLODGroup);
                LODGroupPoolUtils.DEFAULT_LOD_AMOUT = LOD_LEVELS;
                LODGroupPoolUtils.PrewarmLODGroupPool(lodGroupPool, LODGROUP_POOL_PREWARM_VALUE);

                c.LodCache = new LODCache(lodGroupPool);
                staticContainer.CacheCleaner.Register(c.LodCache);

                c.LODPlugin = new LODPlugin(realmData,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.ScenesCache, debugBuilder, staticContainer.SceneReadinessReportQueue,
                    visualSceneStateResolver, textureArrayContainerFactory, c.lodSettingsAsset.Value,
                    staticContainer.RealmPartitionSettings, c.LodCache, lodGroupPool, decentralandUrlsSource, new GameObject("LOD_CACHE").transform, lodEnabled, LOD_LEVELS);

                return UniTask.CompletedTask;
            });
        }

        public override void Dispose()
        {
            roadSettingsAsset.Dispose();
            lodSettingsAsset.Dispose();

            realmData.RealmType.OnUpdate -= SwitchRoadsInstancedRendering;
        }

        protected override async UniTask InitializeInternalAsync(LODContainerSettings lodContainerSettings, CancellationToken ct)
        {
            roadSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(lodContainerSettings.RoadData, ct: ct);
            lodSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(lodContainerSettings.LODSettingAsset, ct: ct);
            roadAssetsPrefabList = new List<GameObject>();

            roadSettingsAsset.Value.CollectGPUInstancingLODGroups();

            foreach (AssetReferenceGameObject? t in roadSettingsAsset.Value.RoadAssetsReference)
            {
                var prefab = await assetsProvisioner.ProvideMainAssetAsync(t, ct: ct);
                // prefab.Value.GetComponent<GPUInstancingPrefabData>().HideVisuals();
                roadAssetsPrefabList.Add(prefab.Value);
            }

            realmData.RealmType.OnUpdate += SwitchRoadsInstancedRendering;
        }

        private void SwitchRoadsInstancedRendering(RealmKind realmKind)
        {
            if (realmKind == RealmKind.GenesisCity)
                gpuInstancingService.AddToIndirect(roadSettingsAsset.Value.IndirectLODGroups);
            else
                gpuInstancingService.Remove(roadSettingsAsset.Value.IndirectLODGroups);
        }

        [Serializable]
        public class LODContainerSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public StaticSettings.RoadDataRef RoadData { get; set; }

            [field: SerializeField]
            public StaticSettings.LODSettingsRef LODSettingAsset { get; set; }
        }
    }
}
