using System.Threading;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.DebugUtilities;
using DCL.LOD;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.PluginSystem.Global
{
    public class LODPlugin : IDCLGlobalPlugin
    {
        private readonly LODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly VisualSceneStateResolver visualSceneStateResolver;
        private readonly TextureArrayContainerFactory textureArrayContainerFactory;
        private GameObjectPool<LODGroup> lodGroupPool;
        private readonly bool lodEnabled;

        private IExtendedObjectPool<Material> lodMaterialPool;
        private ILODSettingsAsset lodSettingsAsset;
        private readonly SceneAssetLock sceneAssetLock;
        private TextureArrayContainer lodTextureArrayContainer;


        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder,
            ISceneReadinessReportQueue sceneReadinessReportQueue, VisualSceneStateResolver visualSceneStateResolver, TextureArrayContainerFactory textureArrayContainerFactory,
            ILODSettingsAsset lodSettingsAsset, SceneAssetLock sceneAssetLock, bool lodEnabled)
        {
            lodAssetsPool = new LODAssetsPool();
            cacheCleaner.Register(lodAssetsPool);

            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.visualSceneStateResolver = visualSceneStateResolver;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
            this.lodSettingsAsset = lodSettingsAsset;
            this.sceneAssetLock = sceneAssetLock;
            this.lodEnabled = lodEnabled;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var lodContainer = new GameObject("POOL_CONTAINER_LODS");
            var lodDebugContainer = new GameObject("POOL_CONTAINER_DEBUG_LODS");
            lodDebugContainer.transform.SetParent(lodContainer.transform);
            lodGroupPool = new GameObjectPool<LODGroup>(lodContainer.transform);

            AsyncInstantiateOperation.SetIntegrationTimeMS(lodSettingsAsset.AsyncIntegrationTimeMS);

            lodTextureArrayContainer = textureArrayContainerFactory.CreateSceneLOD(SCENE_TEX_ARRAY_SHADER, lodSettingsAsset.DefaultTextureArrayResolutionDescriptors,
                TextureFormat.BC7, lodSettingsAsset.ArraySizeForMissingResolutions, lodSettingsAsset.CapacityForMissingResolutions);

            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset, visualSceneStateResolver, realmData);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool, lodSettingsAsset, visualSceneStateResolver, sceneAssetLock);

            if (lodEnabled)
            {
                UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodGroupPool, lodAssetsPool, lodSettingsAsset, scenesCache, sceneReadinessReportQueue, lodContainer.transform);
                UnloadSceneLODSystem.InjectToWorld(ref builder, scenesCache);
                InstantiateSceneLODInfoSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, lodGroupPool, lodAssetsPool, scenesCache, sceneReadinessReportQueue, lodTextureArrayContainer, lodContainer.transform);
                LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, lodDebugContainer.transform);
            }
            else
            {
                UpdateSceneLODInfoMockSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, scenesCache);
            }

        }

        public void Dispose()
        {
            lodAssetsPool.Unload(frameCapBudget, 3);
            lodGroupPool?.Dispose();
        }

    }

}
