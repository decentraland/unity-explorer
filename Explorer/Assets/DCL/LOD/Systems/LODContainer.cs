using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.DebugUtilities;
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
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.LOD.Systems
{
    /// <summary>
    /// LOD Container unites LOD and Road Plugins and their common dependencies
    /// </summary>
    public class LODContainer : DCLContainer<LODContainer.Settings>
    {
        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField]
            public StaticSettings.RoadDataRef RoadData { get; set; }
        }

        private readonly IAssetsProvisioner assetsProvisioner;

        private ProvidedAsset<RoadSettingsAsset> roadSettingsAsset;
        private List<GameObject> roadAssetsPrefabList;

        public LODPlugin LODPlugin { get; private set; } = null!;

        public RoadPlugin RoadPlugin { get; private set; } = null!;

        private LODContainer(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public static async UniTask<(LODContainer? container, bool success)> CreateAsync(
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            RealmData realmData,
            TextureArrayContainerFactory textureArrayContainerFactory,
            DebugContainerBuilder debugBuilder,
            CancellationToken ct)
        {
            var container = new LODContainer(staticContainer.AssetsProvisioner);

            return await container.InitializeContainerAsync<LODContainer, Settings>(settingsContainer, ct, c =>
            {
                var roadDataDictionary = new Dictionary<Vector2Int, RoadDescription>();

                foreach (var roadDescription in c.roadSettingsAsset.Value.RoadDescriptions)
                    roadDataDictionary.Add(roadDescription.RoadCoordinate, roadDescription);

                var visualSceneStateResolver = new VisualSceneStateResolver(roadDataDictionary.Keys.ToHashSet());
                
                // Create plugins
                c.RoadPlugin = new RoadPlugin(staticContainer.CacheCleaner,
                    staticContainer.AssetsProvisioner,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.SingletonSharedDependencies.MemoryBudget, c.roadAssetsPrefabList.AsReadOnly(), roadDataDictionary);

                c.LODPlugin = new LODPlugin(staticContainer.CacheCleaner, realmData,
                    staticContainer.SingletonSharedDependencies.MemoryBudget,
                    staticContainer.SingletonSharedDependencies.FrameTimeBudget,
                    staticContainer.ScenesCache, debugBuilder, staticContainer.AssetsProvisioner, staticContainer.SceneReadinessReportQueue, visualSceneStateResolver, textureArrayContainerFactory);

                return UniTask.CompletedTask;
            });
        }

        public override void Dispose()
        {
            roadSettingsAsset.Dispose();
        }

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            roadSettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.RoadData, ct: ct);
            roadAssetsPrefabList = new List<GameObject>();
            foreach (var t in roadSettingsAsset.Value.RoadAssetsReference)
                roadAssetsPrefabList.Add((await assetsProvisioner.ProvideMainAssetAsync(t, ct: ct)).Value);
        }
    }
}
