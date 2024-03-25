using Arch.Core;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using DCL.SDKComponents.NFTShape.System;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using DCL.WebRequests.WebContentSizes;
using DCL.WebRequests.WebContentSizes.Sizes;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class NFTShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public NFTShapeDemoWorld(World world, IFramesPool framesPool, params (PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)[] list) : this(world, framesPool, list.AsReadOnly()) { }

        public NFTShapeDemoWorld(World world, IFramesPool framesPool, IReadOnlyList<(PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)> list)
        {
            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBNftShape nftShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                        w.Create(nftShape, visibility, billboard, NewTransform(), new PartitionComponent { IsBehind = false, RawSqrDistance = 0 });
                },
                w => new AssetsDeferredLoadingSystem(w, new NullPerformanceBudget(), new NullPerformanceBudget()),
                w => new LoadNFTShapeSystem(
                    w,
                    new NftShapeCache(),
                    new WebRequestController(
                        new MemoryWeb3IdentityCache()
                    ),
                    new MutexSync(),
                    new IWebContentSizes.Default(
                        new MaxSize
                        {
                            maxSizeInBytes = 300 * 1024 * 1024
                        }
                    )
                ).InitializeAndReturnSelf(),
                w => new LoadCycleNftShapeSystem(w, new BasedURNSource()),
                w => new InstantiateNftShapeSystem(w, new PoolNFTShapeRendererFactory(new ComponentPoolsRegistry(), framesPool), new FrameTimeCapBudget.Default()),
                w => new VisibilityNftShapeSystem(w)
            );
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("nft test"));

        public void SetUp() =>
            origin.SetUp();

        public void Update() =>
            origin.Update();
    }
}
