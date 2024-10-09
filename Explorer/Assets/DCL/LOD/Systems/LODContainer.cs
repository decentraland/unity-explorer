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
using UnityEngine;

namespace DCL.LOD.Systems
{
    /// <summary>
    /// LOD Container unites LOD and Road Plugins and their common dependencies
    /// </summary>
    public class LODContainer : DCLWorldContainer<LODContainer.LODContainerSettings>
    {
        [Serializable]
        public class LODContainerSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public StaticSettings.RoadDataRef RoadData { get; set; }
        }

        private readonly IAssetsProvisioner assetsProvisioner;

        private ProvidedAsset<RoadSettingsAsset> roadSettingsAsset;
        private List<GameObject> roadAssetsPrefabList;

        public LODPlugin LODPlugin { get; private set; } = null!;

        public RoadPlugin RoadPlugin { get; private set; } = null!;

        public ILODCache LodCache { get; private set; } = null!;

        private const int LOD_LEVELS = 2;
        private const int LODGROUP_POOL_PREWARM_VALUE = 500;
        private LODContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public static async UniTask<(LODContainer? container, bool success)> CreateAsync(
            IAssetsProvisioner assetsProvisioner,
            IDecentralandUrlsSource decentralandUrlsSource,
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            RealmData realmData,
            TextureArrayContainerFactory textureArrayContainerFactory,
            IDebugContainerBuilder debugBuilder,
            bool lodEnabled,
            CancellationToken ct)
        {
            var container = new LODContainer(assetsProvisioner);

            return await container.InitializeContainerAsync<LODContainer, LODContainerSettings>(settingsContainer, ct, c =>
            {
                var roadDataDictionary = new Dictionary<Vector2Int, RoadDescription>();

                foreach (var roadDescription in c.roadSettingsAsset.Value.RoadDescriptions)
                    roadDataDictionary.Add(roadDescription.RoadCoordinate, roadDescription);

                var visualSceneStateResolver = new VisualSceneStateResolver(roadDataDictionary.Keys.ToHashSet());

                // Create plugins
                c.RoadPlugin = new RoadPlugin(staticContainer.CacheCleaner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget, c.roadAssetsPrefabList, roadDataDictionary,
                    staticContainer.ScenesCache, staticContainer.SceneReadinessReportQueue, staticContainer.ComponentsContainer.ComponentPoolsRegistry);

                IComponentPool<LODGroup> lodGroupPool = staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddGameObjectPool(LODGroupPoolUtils.CreateLODGroup, onRelease: LODGroupPoolUtils.ReleaseLODGroup);
                LODGroupPoolUtils.DEFAULT_LOD_AMOUT = LOD_LEVELS;
                LODGroupPoolUtils.PrewarmLODGroupPool(lodGroupPool, LODGROUP_POOL_PREWARM_VALUE);

                c.LodCache = new LODCache(lodGroupPool);
                staticContainer.CacheCleaner.Register(c.LodCache);

                c.LODPlugin = new LODPlugin(realmData,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.ScenesCache, debugBuilder, staticContainer.SceneReadinessReportQueue,
                    visualSceneStateResolver, textureArrayContainerFactory, staticContainer.LODSettings, staticContainer.SingletonSharedDependencies.SceneAssetLock,
                    staticContainer.RealmPartitionSettings, c.LodCache,lodGroupPool, decentralandUrlsSource,new GameObject("LOD_CACHE").transform, lodEnabled, LOD_LEVELS);

                return UniTask.CompletedTask;
            });
        }

        public override void Dispose()
        {
            roadSettingsAsset.Dispose();
        }

        protected override async UniTask InitializeInternalAsync(LODContainerSettings lodContainerSettings, CancellationToken ct)
        {
            roadSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(lodContainerSettings.RoadData, ct: ct);
            roadAssetsPrefabList = new List<GameObject>();
            foreach (var t in roadSettingsAsset.Value.RoadAssetsReference)
                roadAssetsPrefabList.Add((await assetsProvisioner.ProvideMainAssetAsync(t, ct: ct)).Value);
        }
    }
}
