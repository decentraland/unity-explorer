using System;
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
using ECS.Prioritization;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.PluginSystem.Global
{
    public class LODPlugin : IDCLGlobalPlugin
    {
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly IRealmPartitionSettings partitionSettings;
        private readonly VisualSceneStateResolver visualSceneStateResolver;
        private readonly TextureArrayContainerFactory textureArrayContainerFactory;
        private GameObjectPool<LODGroup> lodGroupPool;
        private readonly bool lodEnabled;

        private IExtendedObjectPool<Material> lodMaterialPool;
        private ILODSettingsAsset lodSettingsAsset;
        private readonly SceneAssetLock sceneAssetLock;
        private TextureArrayContainer lodTextureArrayContainer;
        private readonly ILODCache lodCache;
        
        private const int LOD_LEVELS = 2;
        private const int LODGROUP_POOL_PREWARM_VALUE = 500;


        public LODPlugin(RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder,
            ISceneReadinessReportQueue sceneReadinessReportQueue, VisualSceneStateResolver visualSceneStateResolver, TextureArrayContainerFactory textureArrayContainerFactory,
            ILODSettingsAsset lodSettingsAsset, SceneAssetLock sceneAssetLock, bool lodEnabled, ILODCache lodCache, IRealmPartitionSettings partitionSettings)
        {
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
            this.partitionSettings = partitionSettings;
            this.lodCache = lodCache;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            lodTextureArrayContainer = textureArrayContainerFactory.CreateSceneLOD(SCENE_TEX_ARRAY_SHADER, lodSettingsAsset.DefaultTextureArrayResolutionDescriptors,
                TextureFormat.BC7, lodSettingsAsset.ArraySizeForMissingResolutions, lodSettingsAsset.CapacityForMissingResolutions);

            lodCache.PrewarmLODGroupPool(LOD_LEVELS, LODGROUP_POOL_PREWARM_VALUE);
            
            CalculateLODBiasSystem.InjectToWorld(ref builder);
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodSettingsAsset, visualSceneStateResolver, realmData);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodCache, lodSettingsAsset, visualSceneStateResolver, sceneAssetLock);

            
            if (lodEnabled)
            {
                InitializeSceneLODInfoSystem.InjectToWorld(ref builder, lodCache, LOD_LEVELS);
                UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodSettingsAsset, scenesCache, sceneReadinessReportQueue);
                UnloadSceneLODSystem.InjectToWorld(ref builder, scenesCache, lodCache);
                InstantiateSceneLODInfoSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, scenesCache, sceneReadinessReportQueue, lodTextureArrayContainer, partitionSettings);
                LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, LOD_LEVELS);
            }
            else
            {
                UpdateSceneLODInfoMockSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, scenesCache);
            }
        }
        

        public void Dispose()
        {
            lodCache.Unload(frameCapBudget, 3);
            lodGroupPool?.Dispose();
        }
    }
}
