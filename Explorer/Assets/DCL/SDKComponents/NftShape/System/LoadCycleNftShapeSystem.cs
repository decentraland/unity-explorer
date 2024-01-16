using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.NftShapes;
using ECS.Unity.Materials;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NftShapes.GetNftShapeIntention>;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(MaterialLoadingGroup))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        public LoadCycleNftShapeSystem(World world) : base(world) { }

        protected override void Update(float t)
        {
            StartQuery(World!);
            FinishAndApplyQuery(World!);
        }

        [Query]
        [None(typeof(NftLoadingComponent))]
        public void Start(in Entity entity, in PBNftShape nftShape, in PartitionComponent partitionComponent)
        {
            var promise = Promise.Create(World!, new GetNftShapeIntention(nftShape.Urn!), partitionComponent);
            World!.Add(entity, new NftLoadingComponent(promise));
        }

        [Query]
        public void FinishAndApply(ref NftLoadingComponent nftLoadingComponent, in NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.readOnlyStatus == LifeCycle.LoadingInProgress
                && nftLoadingComponent.promise.TryGetResult(World!, out var result)
               )
            {
                nftLoadingComponent.Finish();

                if (result.Succeeded)
                    nftShapeRendererComponent.PoolableComponent.Apply(result.Asset);
                else
                    nftShapeRendererComponent.PoolableComponent.NotifyFailed();

                nftLoadingComponent.Applied();
            }
        }
    }
}
