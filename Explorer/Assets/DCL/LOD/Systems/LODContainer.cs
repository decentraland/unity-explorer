using Cysharp.Threading.Tasks;
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
using DCL.Rendering.GPUInstancing;
using DCL.Roads;
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
        private RoadsPresence roadsPresence;

        public LODPlugin LODPlugin { get; private set; } = null!;

        public RoadPlugin RoadPlugin { get; private set; } = null!;

        public RoadAssetsPool RoadAssetsPool { get; private set; } = null!;

        public ILODCache LodCache { get; private set; } = null!;

        public ILODSettingsAsset LODSettings { get; private set; } = null!;

        public HashSet<Vector2Int> RoadCoordinates { get; private set; }

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
            container.roadsPresence = new RoadsPresence(realmData, gpuInstancingService);

            return await container.InitializeContainerAsync<LODContainer, LODContainerSettings>(settingsContainer, ct, c =>
            {
                var roadDataDictionary = new Dictionary<Vector2Int, RoadDescription>();

                foreach (RoadDescription roadDescription in c.roadSettingsAsset.Value.RoadDescriptions)
                    roadDataDictionary.Add(roadDescription.RoadCoordinate, roadDescription);

                container.RoadCoordinates = roadDataDictionary.Keys.ToHashSet();

                var roadAssetPool = new RoadAssetsPool(realmData, c.roadAssetsPrefabList, staticContainer.ComponentsContainer.ComponentPoolsRegistry);
                container.RoadAssetsPool = roadAssetPool;
                staticContainer.CacheCleaner.Register(roadAssetPool);

                // Create plugins
                c.RoadPlugin = new RoadPlugin(staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    roadDataDictionary,
                    staticContainer.ScenesCache,
                    staticContainer.SceneReadinessReportQueue,
                    roadAssetPool,
                    gpuInstancingService,
                    debugBuilder);

                IComponentPool<LODGroup> lodGroupPool = staticContainer.ComponentsContainer.ComponentPoolsRegistry.AddGameObjectPool(LODGroupPoolUtils.CreateLODGroup, onRelease: LODGroupPoolUtils.ReleaseLODGroup);
                LODGroupPoolUtils.DEFAULT_LOD_AMOUT = LOD_LEVELS;
                LODGroupPoolUtils.PrewarmLODGroupPool(lodGroupPool, LODGROUP_POOL_PREWARM_VALUE);

                c.LodCache = new LODCache(lodGroupPool);
                c.LODSettings = c.lodSettingsAsset.Value;
                staticContainer.CacheCleaner.Register(c.LodCache);

                c.LODPlugin = new LODPlugin(
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.ScenesCache, debugBuilder, staticContainer.SceneReadinessReportQueue,
                    textureArrayContainerFactory, c.lodSettingsAsset.Value,
                    staticContainer.RealmPartitionSettings, c.LodCache, lodGroupPool, decentralandUrlsSource, new GameObject("LOD_CACHE").transform, lodEnabled, LOD_LEVELS, staticContainer.GltfContainerAssetsCache);

                return UniTask.CompletedTask;
            });
        }

        public override void Dispose()
        {
            roadSettingsAsset.Dispose();
            lodSettingsAsset.Dispose();
            roadsPresence.Dispose();
        }

        protected override async UniTask InitializeInternalAsync(LODContainerSettings lodContainerSettings, CancellationToken ct)
        {
            roadSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(lodContainerSettings.RoadData, ct: ct);
            lodSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(lodContainerSettings.LODSettingAsset, ct: ct);
            roadAssetsPrefabList = new List<GameObject>();

            foreach (AssetReferenceGameObject? t in roadSettingsAsset.Value.RoadAssetsReference)
            {
                var prefab = await assetsProvisioner.ProvideMainAssetAsync(t, ct: ct);
                roadAssetsPrefabList.Add(prefab.Value);
            }

            roadsPresence.Initialize(roadSettingsAsset.Value);
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
