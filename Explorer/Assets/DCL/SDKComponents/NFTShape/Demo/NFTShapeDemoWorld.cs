#if UNITY_EDITOR

using Arch.Core;
using DCL.Browser.DecentralandUrls;
using DCL.DemoWorlds;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using DCL.SDKComponents.MediaStream;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.Frames.FramePrefabs;
using DCL.SDKComponents.NFTShape.Frames.Pool;
using DCL.SDKComponents.NFTShape.Renderer.Factory;
using DCL.SDKComponents.NFTShape.System;
using DCL.Utilities.Extensions;
using DCL.WebRequests;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.DeferredLoading;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Transforms.Components;
using Global.Dynamic.LaunchModes;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Demo
{
    public class NFTShapeDemoWorld : IDemoWorld
    {
        private readonly IDemoWorld origin;

        public NFTShapeDemoWorld(World world, IFramesPool framesPool,
            IReadOnlyFramePrefabs framePrefabs, IComponentPool<PartitionComponent> partitionComponentPool,
            params (PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)[] list)
            : this(world, framesPool, framePrefabs, partitionComponentPool, list.AsReadOnly()) { }

        public NFTShapeDemoWorld(World world, IFramesPool framesPool,
            IReadOnlyFramePrefabs framePrefabs, IComponentPool<PartitionComponent> partitionComponentPool,
            IReadOnlyList<(PBNftShape textShape, PBVisibilityComponent visibility, PBBillboard billboard)> list)
        {
            var buffer = new EntityEventBuffer<NftShapeRendererComponent>(1);

            IWebRequestController webRequestController = IWebRequestController.DEFAULT;

            origin = new DemoWorld(
                world,
                w =>
                {
                    foreach ((PBNftShape nftShape, PBVisibilityComponent visibility, PBBillboard billboard) in list)
                    {
                        PartitionComponent partitionComponent = partitionComponentPool.Get();
                        partitionComponent.IsBehind = false;
                        partitionComponent.RawSqrDistance = 0f;

                        w.Create(nftShape, visibility, billboard, NewTransform(), partitionComponent);
                    }
                },
                w => new AssetsDeferredLoadingSystem(w, new NullPerformanceBudget(), new NullPerformanceBudget()),
                w => new LoadNFTTypeSystem(
                    w,
                    new NoCache<NftTypeResult, GetNFTTypeIntention>(false, false),
                    webRequestController,
                    true,
                    new DecentralandUrlsSource(DecentralandEnvironment.Zone, ILaunchMode.PLAY)
                ).InitializeAndReturnSelf(),
                w => new LoadNFTImageSystem(w, new TexturesCache<GetNFTImageIntention>()),
                w => new LoadCycleNftShapeSystem(w, new BasedURNSource(new DecentralandUrlsSource(DecentralandEnvironment.Org, ILaunchMode.PLAY)), new MockMediaFactory()),
                w => new InstantiateNftShapeSystem(w, new PoolNFTShapeRendererFactory(new ComponentPoolsRegistry(), framesPool), new FrameTimeCapBudget.Default(), framePrefabs, buffer),
                w => new VisibilityNftShapeSystem(w, buffer)
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

#endif
