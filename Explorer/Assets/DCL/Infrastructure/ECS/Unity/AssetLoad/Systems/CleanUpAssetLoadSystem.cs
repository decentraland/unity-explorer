using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
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
        [All(typeof(DeleteEntityIntention), typeof(AssetLoadComponent))]
        private void HandleComponentRemoval(in CRDTEntity sdkEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAssetLoadLoadingState>(sdkEntity);
        }

        public void FinalizeComponents(in Query query)
        {
            HandleComponentRemovalQuery(World);
            assetLoadCache.Dispose();
        }
    }
}
