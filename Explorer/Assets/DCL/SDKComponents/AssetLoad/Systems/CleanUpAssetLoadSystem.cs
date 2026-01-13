using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECS.Unity.AssetLoad.Cache;
using DCL.ECSComponents;
using DCL.SDKComponents.AssetLoad.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace DCL.SDKComponents.AssetLoad.Systems
{
    /// <summary>
    ///     Cleans up asset loading when PBAssetLoad component is removed
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateBefore(typeof(AssetLoadSystem))]
    [ThrottlingEnabled]
    public partial class CleanUpAssetLoadSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly AssetLoadCache assetLoadCache;

        internal CleanUpAssetLoadSystem(World world,
            IECSToCRDTWriter ecsToCRDTWriter,
            AssetLoadCache assetLoadCache)
            : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.assetLoadCache = assetLoadCache;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(ref AssetLoadComponent component, CRDTEntity sdkEntity)
        {
            // Cancel and destroy all loading entities
            foreach (var kvp in component.LoadingEntities)
                AssetLoadUtils.RemoveAssetLoading(World, kvp.Value, kvp.Key, ref component);

            // Delete loading state message
            ecsToCRDTWriter.DeleteMessage<PBAssetLoadLoadingState>(sdkEntity);
        }

        public void FinalizeComponents(in Query query)
        {
            HandleComponentRemovalQuery(World);
            assetLoadCache.Dispose();
        }
    }
}
