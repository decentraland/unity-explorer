using System;
using System.Collections.Generic;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.DebugUtilities;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.ResourcesUnloading;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using Utility;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.LOD
{
    public class LODPlugin : IDCLGlobalPlugin<LODSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly LODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly VisualSceneStateResolver visualSceneStateResolver;
        private readonly TextureArrayContainerFactory textureArrayContainerFactory;

        
        private IExtendedObjectPool<Material> lodMaterialPool;
        private ILODSettingsAsset lodSettingsAsset;
        private Dictionary<TextureFormat, TextureArrayContainer> textureArrayDictionary;


        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder, IAssetsProvisioner assetsProvisioner,
            ISceneReadinessReportQueue sceneReadinessReportQueue, VisualSceneStateResolver visualSceneStateResolver, TextureArrayContainerFactory textureArrayContainerFactory)
        {
            lodAssetsPool = new LODAssetsPool();
            cacheCleaner.Register(lodAssetsPool);

            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;
            this.assetsProvisioner = assetsProvisioner;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
        }

        public async UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodSettingsAsset = (await assetsProvisioner.ProvideMainAssetAsync(settings.LODSettingAsset, ct: ct)).Value;
            await CreateMaterialPoolPrewarmedAsync(lodSettingsAsset, ct);
        }

        //TODO: (Juani) All of this hastle can be removed if we put the material directly in the AB converter
        private async UniTask CreateMaterialPoolPrewarmedAsync(ILODSettingsAsset settings, CancellationToken ct)
        {
            var providedMaterial = await assetsProvisioner.ProvideMainAssetAsync(settings.DefaultLODMaterial, ct: ct);

            lodMaterialPool = new ExtendedObjectPool<Material>(
                () => new Material(providedMaterial.Value),
                actionOnRelease: mat =>
                {
                    // reset material so it does not contain any old properties
                    mat.CopyPropertiesFromMaterial(providedMaterial.Value);
                },
                actionOnDestroy: UnityObjectUtils.SafeDestroy,
                defaultCapacity: 250);

            var prewarmedMaterials = new Material[250];
            for (int i = 0; i < 250; i++)
                prewarmedMaterials[i] = lodMaterialPool.Get();

            for (int i = 0; i < 250; i++)
                lodMaterialPool.Release(prewarmedMaterials[i]);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            var lodContainer = new GameObject("POOL_CONTAINER_LODS");
            var lodDebugContainer = new GameObject("POOL_CONTAINER_DEBUG_LODS");
            lodDebugContainer.transform.SetParent(lodContainer.transform);

            textureArrayDictionary = new Dictionary<TextureFormat, TextureArrayContainer>();
            foreach (var textureFormat in lodSettingsAsset.FormatsToCreate)
                textureArrayDictionary.Add(textureFormat,
                    textureArrayContainerFactory.Create(SCENE_TEX_ARRAY_SHADER, lodSettingsAsset.DefaultTextureArrayResolutions, textureFormat, lodSettingsAsset.TextureArrayMinSize));
            
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset, visualSceneStateResolver, realmData);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool, lodSettingsAsset, visualSceneStateResolver);
            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodAssetsPool, lodSettingsAsset, memoryBudget,
                frameCapBudget, scenesCache, sceneReadinessReportQueue, lodContainer.transform, lodMaterialPool, textureArrayDictionary);
            UnloadSceneLODSystem.InjectToWorld(ref builder, lodAssetsPool, scenesCache);
            LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, lodDebugContainer.transform);
        }

        public void Dispose()
        {
            lodAssetsPool.Unload(frameCapBudget, 3);
        }
    }

    [Serializable]
    public class LODSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LODPlugin) + "." + nameof(LODSettings))]
        [field: Space]
        [field: SerializeField]
        public StaticSettings.LODSettingsRef LODSettingAsset { get; set; }
        
    }
}
