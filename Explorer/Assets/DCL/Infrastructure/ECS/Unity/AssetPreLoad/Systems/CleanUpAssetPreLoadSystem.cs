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
    [UpdateAfter(typeof(HandleAssetPreLoadUpdates))]
    [ThrottlingEnabled]
    public partial class CleanUpAssetPreLoadSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly AssetPreLoadCache assetPreLoadCache;

        internal CleanUpAssetPreLoadSystem(World world,
            AssetPreLoadCache assetPreLoadCache)
            : base(world)
        {
            this.assetPreLoadCache = assetPreLoadCache;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            DestroyCompletedEntitiesQuery(World);
        }

        [Query]
        private void DestroyCompletedEntities(in Entity entity, ref AssetPreLoadLoadingStateComponent loadingStateComponent)
        {
            if (loadingStateComponent.IsDirty) return;
            // Only destroy entities that have finished loading
            if (loadingStateComponent.LoadingState is LoadingState.Loading or LoadingState.Unknown) return;

            World.Destroy(entity);
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
            assetPreLoadCache.Clear();
        }
    }
}
