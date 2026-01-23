using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.AssetLoad.Components;

namespace ECS.Unity.AssetLoad.Systems
{
    /// <summary>
    ///     Cleans up asset loading when PBAssetLoad component is removed
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [ThrottlingEnabled]
    public partial class CleanUpAssetPreLoadSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly AssetPreLoadCache assetPreLoadCache;
        private readonly AssetPreLoadUtils assetPreLoadUtils;

        internal CleanUpAssetPreLoadSystem(World world,
            AssetPreLoadCache assetPreLoadCache,
            AssetPreLoadUtils assetPreLoadUtils)
            : base(world)
        {
            this.assetPreLoadCache = assetPreLoadCache;
            this.assetPreLoadUtils = assetPreLoadUtils;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBAssetLoad))]
        [All(typeof(AssetPreLoadComponent))]
        private void HandleComponentRemoval(in Entity entity)
        {
            World.Remove<AssetPreLoadComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            assetPreLoadCache.Dispose();
            assetPreLoadUtils.Dispose();
        }
    }
}
