using System;
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
        private int sceneLodLimit;
        private Vector2Int[] lodBucketLimits;

        private readonly LODAssetCache lodCache;
        private readonly IScenesCache scenesCache;
        private IRealmData realmData;
        private readonly IPerformanceBudget frameCapBudget;
        private readonly IPerformanceBudget memoryBudget;


        public LODPlugin(CacheCleaner cacheCleaner, RealmData realmData, IPerformanceBudget memoryBudget,
            IPerformanceBudget frameCapBudget, IScenesCache scenesCache)
        {
            lodCache = new LODAssetCache();
            cacheCleaner.Register(lodCache);
            this.realmData = realmData;
            this.memoryBudget = memoryBudget;
            this.frameCapBudget = frameCapBudget;
            this.scenesCache = scenesCache;
        }

        public UniTask InitializeAsync(LODSettings settings, CancellationToken ct)
        {
            lodBucketLimits = settings.LodBucketLimits;
            sceneLodLimit = settings.SceneLODLimit;
            return default;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            ResolveVisualSceneStateSystem.InjectToWorld(ref builder, sceneLodLimit);
            UpdateVisualSceneStateSystem.InjectToWorld(ref builder, realmData, scenesCache);
            ResolveSceneLODInfo.InjectToWorld(ref builder, lodCache);
            UpdateSceneLODInfoSystem.InjectToWorld(ref builder, lodCache, lodBucketLimits, frameCapBudget,
                memoryBudget);
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
        public int SceneLODLimit { get; private set; } = 1;

        [field: SerializeField] public Vector2Int[] LodBucketLimits = { new(1, 2), new(2, 5) };
    }
}