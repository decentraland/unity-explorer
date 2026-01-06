using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.AssetLoad.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.AssetBundles;
using System;

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

        internal CleanUpAssetLoadSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(Entity entity, ref AssetLoadComponent component, CRDTEntity sdkEntity)
        {
            // Cancel and destroy all loading entities
            foreach (var kvp in component.LoadingEntities)
            {
                Entity loadingEntity = kvp.Value;
                if (World.IsAlive(loadingEntity))
                {
                    if (World.TryGet(loadingEntity, out GetAssetBundleIntention intention))
                    {
                        intention.CancellationTokenSource?.Cancel();
                    }
                    World.Destroy(loadingEntity);
                }
            }

            // Delete loading state message
            ecsToCRDTWriter.DeleteMessage<PBAssetLoadLoadingState>(sdkEntity);
        }

        public void FinalizeComponents(in Query query)
        {
            HandleComponentRemovalQuery(World);
        }
    }
}
