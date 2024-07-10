using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.Unity.Groups;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.System
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateAfter(typeof(InstantiateNftShapeSystem))]
    [ThrottlingEnabled]
    public partial class LoadCycleNftShapeSystem : BaseUnityLoopSystem
    {
        private readonly IURNSource urnSource;

        public LoadCycleNftShapeSystem(World world, IURNSource urnSource) : base(world)
        {
            this.urnSource = urnSource;
        }

        protected override void Update(float t)
        {
            StartQuery(World!);
            FinishAndApplyQuery(World!);
        }

        [Query]
        [None(typeof(NFTLoadingComponent))]
        private void Start(in Entity entity, in PBNftShape nftShape) //TODO optimise partitioning
        {
            var promise = Promise.Create(World!, new GetNFTShapeIntention(nftShape.Urn!, urnSource), PartitionComponent.TOP_PRIORITY);
            World!.Add(entity, new NFTLoadingComponent(promise));
        }

        [Query]
        private void FinishAndApply(ref NFTLoadingComponent nftLoadingComponent, in NftShapeRendererComponent nftShapeRendererComponent)
        {
            if (nftLoadingComponent.TryGetResult(World!, out var result))
            {
                if (result.Succeeded)
                    nftShapeRendererComponent.PoolableComponent.Apply(result.Asset);
                else
                    nftShapeRendererComponent.PoolableComponent.NotifyFailed();
            }
        }
    }
}
