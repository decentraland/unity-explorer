using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World;
using DCL.ResourcesUnloading;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.LOD
{
    public class LODPlugin : IDCLGlobalPlugin<LODSettings>
    {
        private List<int> lodBucketThresholds;

        private readonly LODAssetsPool lodAssetsPool;
        private readonly IScenesCache scenesCache;
        private IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;


        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache)
        {
            lodAssetsPool = new LODAssetsPool();
            cacheCleaner.Register(lodAssetsPool);
            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
        }

        public UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodBucketThresholds = settings.LodPartitionBucketThresholds.ToList();
            return default;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, lodBucketThresholds[0]);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache, lodAssetsPool);
            ResolveSceneLODInfo.InjectToWorld(ref builder, lodAssetsPool);

            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodAssetsPool, lodBucketThresholds, memoryBudget,
                frameCapBudget);

            UnloadSceneLODSystem.InjectToWorld(ref builder, lodAssetsPool);
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }
    }

    [Serializable]
    public class LODSettings : IDCLPluginSettings
    {
        [field: Header(nameof(LODPlugin) + "." + nameof(LODSettings))]
        [field: Space]
        [field: SerializeField]
        public int[] LodPartitionBucketThresholds = { 1, 2, 5 };
    }
}
