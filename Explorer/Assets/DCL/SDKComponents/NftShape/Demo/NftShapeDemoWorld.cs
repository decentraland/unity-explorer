using Arch.Core;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.NftShape.Frames.Pool;
using DCL.SDKComponents.NftShape.Renderer.Factory;
using DCL.SDKComponents.NftShape.System;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.NftShapes;
using ECS.StreamableLoading.NftShapes.Urns;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility.Multithreading;

namespace DCL.SDKComponents.NftShape.Demo
{
    public class NftShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public NftShapeDemoWorld(World world, IFramesPool framesPool, params (PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)[] list) : this(world, framesPool, list.AsReadOnly()) { }

        public NftShapeDemoWorld(World world, IFramesPool framesPool, IReadOnlyList<(PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)> list)
        {
            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBNftShape nftShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                        w.Create(nftShape, visibility, billboard, NewTransform(), new PartitionComponent { IsBehind = false, RawSqrDistance = 0 });
                },
                w => new AssetsDeferredLoadingSystem(w, new NullPerformanceBudget(), new NullPerformanceBudget()),
                w => new LoadNftShapeSystem(w, new NftShapeCache(), new WebRequestController(new WebRequestsAnalyticsContainer(), new MemoryWeb3IdentityCache()), new MutexSync()).InitializeAndReturnSelf(),
                w => new LoadCycleNftShapeSystem(w, new BasedUrnSource()),
                w => new InstantiateNftShapeSystem(w, new PoolNftShapeRendererFactory(new ComponentPoolsRegistry(), framesPool)),
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
