using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Rendering.TextureArray;
using DCL.DebugUtilities;
using DCL.LOD;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.DecentralandUrls;
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
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;
        private readonly IDebugContainerBuilder debugBuilder;
        private readonly ISceneReadinessReportQueue sceneReadinessReportQueue;
        private readonly IRealmPartitionSettings partitionSettings;
        private readonly TextureArrayContainerFactory textureArrayContainerFactory;

        private IExtendedObjectPool<Material> lodMaterialPool;
        private readonly ILODSettingsAsset lodSettingsAsset;
        private TextureArrayContainer lodTextureArrayContainer;
        private readonly CacheCleaner cacheCleaner;

        private readonly ILODCache lodCache;
        private readonly IComponentPool<LODGroup> lodGroupPool;

        private readonly bool lodEnabled;
        private readonly int lodLevels;
        private readonly Transform lodCacheParent;

        public LODPlugin(IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache, IDebugContainerBuilder debugBuilder,
            ISceneReadinessReportQueue sceneReadinessReportQueue, TextureArrayContainerFactory textureArrayContainerFactory,
            ILODSettingsAsset lodSettingsAsset, IRealmPartitionSettings partitionSettings,
            ILODCache lodCache, IComponentPool<LODGroup> lodGroupPool, IDecentralandUrlsSource decentralandUrlsSource, Transform lodCacheParent, bool lodEnabled,
            int lodLevels)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
            this.debugBuilder = debugBuilder;
            this.sceneReadinessReportQueue = sceneReadinessReportQueue;
            this.textureArrayContainerFactory = textureArrayContainerFactory;
            this.lodSettingsAsset = lodSettingsAsset;
            this.lodEnabled = lodEnabled;
            this.partitionSettings = partitionSettings;
            this.lodCache = lodCache;
            this.lodGroupPool = lodGroupPool;
            this.lodCacheParent = lodCacheParent;
            this.lodLevels = lodLevels;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            lodTextureArrayContainer = textureArrayContainerFactory.CreateSceneLOD(SCENE_TEX_ARRAY_SHADER, lodSettingsAsset.DefaultTextureArrayResolutionDescriptors,
                TextureFormat.BC7, lodSettingsAsset.ArraySizeForMissingResolutions, lodSettingsAsset.CapacityForMissingResolutions);

            if (lodEnabled)
            {
                CalculateLODBiasSystem.InjectToWorld(ref builder);
                RecalculateLODDistanceSystem.InjectToWorld(ref builder, partitionSettings);

                InitializeSceneLODInfoSystem.InjectToWorld(ref builder, lodCache, lodLevels, lodGroupPool,
                    lodCacheParent, sceneReadinessReportQueue, scenesCache);

                UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodSettingsAsset, decentralandUrlsSource);
                InstantiateSceneLODInfoSystem.InjectToWorld(ref builder, frameCapBudget, memoryBudget, scenesCache, sceneReadinessReportQueue, lodTextureArrayContainer, partitionSettings);
                LODDebugToolsSystem.InjectToWorld(ref builder, debugBuilder, lodSettingsAsset, lodLevels);
            }
            else
                UpdateSceneLODInfoMockSystem.InjectToWorld(ref builder, sceneReadinessReportQueue, scenesCache);
        }

        public void Dispose()
        {
            lodCache.Unload(new NullPerformanceBudget(), int.MaxValue);
            lodGroupPool?.Dispose();
        }
    }
}
