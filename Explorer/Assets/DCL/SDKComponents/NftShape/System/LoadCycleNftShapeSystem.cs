using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NftShape.Component;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.NftShapes;
using ECS.StreamableLoading.NftShapes.Urns;
using ECS.Unity.Groups;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NftShapes.GetNftShapeIntention>;

namespace DCL.SDKComponents.NftShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateNftShapeSystem))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly IUrnSource urnSource;

        public LoadCycleNftShapeSystem(World world, IUrnSource urnSource) : base(world)
        {
            this.urnSource = urnSource;
        }

        protected override void Update(float t)
        {
            StartQuery(World!);
            FinishAndApplyQuery(World!);
        }

        [Query]
        [None(typeof(NftLoadingComponent))]
        private void Start(in Entity entity, in PBNftShape nftShape, in PartitionComponent partitionComponent)
        {
            var promise = Promise.Create(World!, new GetNftShapeIntention(nftShape.Urn!, urnSource), partitionComponent);
            World!.Add(entity, new NftLoadingComponent(promise));
        }

        [Query]
        private void FinishAndApply(ref NftLoadingComponent nftLoadingComponent, in NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.TryGetResult(World!, out var result))
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
