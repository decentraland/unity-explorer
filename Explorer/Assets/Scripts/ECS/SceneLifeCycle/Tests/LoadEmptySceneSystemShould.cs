using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Optimization.Pools;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.Analytics;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.Tests
{
    public class LoadEmptySceneSystemShould
    {
        private static readonly URLAddress EMPTY_SCENES_MAPPINGS_URL =
            URLAddress.FromString($"file://{Application.streamingAssetsPath}/EmptyScenes/mappings.json");

        private LoadEmptySceneSystemLogic loadEmptySceneSystemLogic;
        private IEmptyScenesWorldFactory emptyScenesWorldFactory;
        private Dictionary<CRDTEntity, Entity> map;

        private static Vector2Int[] Parcels => new[]
        {
            Vector2Int.zero,
            new Vector2Int(5, 5),
            new Vector2Int(-5, 5),
            new Vector2Int(-15, 34),
            new Vector2Int(180, 9091),
        };


        public void SetUp()
        {
            loadEmptySceneSystemLogic = new LoadEmptySceneSystemLogic(
                TestSuite.TestWebRequestController.INSTANCE,
                emptyScenesWorldFactory = Substitute.For<IEmptyScenesWorldFactory>(),
                Substitute.For<IComponentPoolsRegistry>(),
                EMPTY_SCENES_MAPPINGS_URL);

            var world = World.Create();
            var builder = new ArchSystemsWorldBuilder<World>(world);

            emptyScenesWorldFactory.Create(Arg.Any<EmptySceneData>()).Returns(new EmptyScenesWorld(builder.Finish(), map = new Dictionary<CRDTEntity, Entity>(), world, new MutexSync()));
        }


        public async Task LoadMapping()
        {
            await loadEmptySceneSystemLogic.LoadMappingAsync(Array.Empty<Vector2Int>(), CancellationToken.None);

            Assert.NotNull(loadEmptySceneSystemLogic.emptySceneData);
            Assert.That(loadEmptySceneSystemLogic.emptySceneData.Mappings.Count, Is.EqualTo(12));
        }



        public async Task CreateSceneFacade(Vector2Int parcel)
        {
            IPartitionComponent partition = Substitute.For<IPartitionComponent>();
            var world = World.Create();

            var intent = new GetSceneFacadeIntention(Substitute.For<IIpfsRealm>(), new SceneDefinitionComponent(parcel));

            ISceneFacade facade = await loadEmptySceneSystemLogic.FlowAsync(world, intent, partition, CancellationToken.None);
            Assert.NotNull(facade);
        }

        //
        // Disabled temporally
        public async Task FacadeCreateEntities()
        {
            IComponentPoolsRegistry pool = Substitute.For<IComponentPoolsRegistry>();

            pool.GetReferenceTypePool<SDKTransform>()
                .Returns(_ =>
                 {
                     IComponentPool<SDKTransform> s = Substitute.For<IComponentPool<SDKTransform>>();
                     s.Get().Returns(_ => new SDKTransform());
                     return s;
                 });

            pool.GetReferenceTypePool<PBGltfContainer>()
                .Returns(_ =>
                 {
                     IComponentPool<PBGltfContainer> s = Substitute.For<IComponentPool<PBGltfContainer>>();
                     s.Get().Returns(_ => new PBGltfContainer());
                     return s;
                 });

            pool.GetReferenceTypePool<PartitionComponent>()
                .Returns(_ =>
                 {
                     IComponentPool<PartitionComponent> s = Substitute.For<IComponentPool<PartitionComponent>>();
                     s.Get().Returns(_ => new PartitionComponent());
                     return s;
                 });

            PartitionComponent partition = PartitionComponent.TOP_PRIORITY;
            var world = World.Create();
            var globalWorld = World.Create();

            var args = new EmptySceneFacade.Args(map, world, globalWorld,
                new EmptySceneMapping { environment = new ContentDefinition { file = "file1" }, grass = new ContentDefinition { file = "file2" } },
                pool, Vector3.one, new SceneShortInfo(Vector2Int.zero, "EMPTY"), partition, new MutexSync());

            var facade = EmptySceneFacade.Create(args);

            await facade.StartUpdateLoopAsync(30, CancellationToken.None);

            Assert.That(map.Count, Is.EqualTo(1));

            // Scene root
            Assert.That(world.CountEntities(new QueryDescription().WithExclusive<SDKTransform>()), Is.EqualTo(1));
            Assert.That(world.Get<SDKTransform>(facade.sceneRoot).Position, Is.EqualTo(Vector3.one));

            Assert.That(world.Get<SDKTransform>(facade.environment).Position, Is.EqualTo(EmptySceneFacade.GLTF_POSITION));

            //Assert.That(world.Get<SDKTransform>(facade.grass).Position, Is.EqualTo(EmptySceneFacade.GLTF_POSITION));
            Assert.That(world.Get<IPartitionComponent>(facade.environment), Is.EqualTo(partition));

            //Assert.That(world.Get<IPartitionComponent>(facade.grass), Is.EqualTo(partition));
            Assert.That(world.Get<PBGltfContainer>(facade.environment).Src, Is.EqualTo("file1"));

            //Assert.That(world.Get<PBGltfContainer>(facade.grass).Src, Is.EqualTo("file2"));
        }
    }
}
