using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using Ipfs;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.EmptyScene;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility.Multithreading;

namespace ECS.SceneLifeCycle.Tests
{
    public class LoadEmptySceneSystemShould
    {
        private static readonly string EMPTY_SCENES_MAPPINGS_URL = Application.streamingAssetsPath + "/EmptyScenes/mappings.json";

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

        [SetUp]
        public void SetUp()
        {
            loadEmptySceneSystemLogic = new LoadEmptySceneSystemLogic(
                emptyScenesWorldFactory = Substitute.For<IEmptyScenesWorldFactory>(),
                Substitute.For<IComponentPoolsRegistry>(),
                EMPTY_SCENES_MAPPINGS_URL);

            var world = World.Create();
            var builder = new ArchSystemsWorldBuilder<World>(world);

            emptyScenesWorldFactory.Create(Arg.Any<EmptySceneData>()).Returns(new EmptyScenesWorld(builder.Finish(), map = new Dictionary<CRDTEntity, Entity>(), world, new MutexSync()));
        }

        [Test]
        public async Task LoadMapping()
        {
            await loadEmptySceneSystemLogic.LoadMapping(CancellationToken.None);

            Assert.NotNull(loadEmptySceneSystemLogic.emptySceneData);
            Assert.That(loadEmptySceneSystemLogic.emptySceneData.Mappings.Count, Is.EqualTo(12));
        }

        [Test]
        [TestCaseSource(nameof(Parcels))]
        public async Task CreateSceneFacade(Vector2Int parcel)
        {
            IPartitionComponent partition = Substitute.For<IPartitionComponent>();

            var intent = new GetSceneFacadeIntention(Substitute.For<IIpfsRealm>(), default(IpfsTypes.IpfsPath), new IpfsTypes.SceneEntityDefinition
                {
                    id = $"empty-parcel-{parcel.x}-{parcel.y}",
                    metadata = new IpfsTypes.SceneMetadata
                    {
                        main = "bin/game.js",
                        scene = SceneDefinitionComponent.EMPTY_METADATA,
                    },

                    // content will be filled by the loading system
                },
                new[] { parcel }, true);

            ISceneFacade facade = await loadEmptySceneSystemLogic.Flow(intent, partition, CancellationToken.None);
            Assert.NotNull(facade);
        }

        [Test]
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

            var args = new EmptySceneFacade.Args(map, world,
                new EmptySceneMapping { environment = new IpfsTypes.ContentDefinition { file = "file1" }, grass = new IpfsTypes.ContentDefinition { file = "file2" } },
                pool, Vector3.one, partition, new MutexSync());

            var facade = EmptySceneFacade.Create(args);

            await facade.StartUpdateLoop(30, CancellationToken.None);

            Assert.That(map.Count, Is.EqualTo(1));

            // Scene root
            Assert.That(world.CountEntities(new QueryDescription().WithExclusive<SDKTransform>()), Is.EqualTo(1));
            Assert.That(world.Get<SDKTransform>(facade.sceneRoot).Position, Is.EqualTo(Vector3.one));

            // 2 entities
            Assert.That(world.CountEntities(new QueryDescription().WithAll<SDKTransform, PBGltfContainer, IPartitionComponent, PartitionComponent>()), Is.EqualTo(2));

            Assert.That(world.Get<SDKTransform>(facade.environment).Position, Is.EqualTo(EmptySceneFacade.GLTF_POSITION));
            Assert.That(world.Get<SDKTransform>(facade.grass).Position, Is.EqualTo(EmptySceneFacade.GLTF_POSITION));
            Assert.That(world.Get<IPartitionComponent>(facade.environment), Is.EqualTo(partition));
            Assert.That(world.Get<IPartitionComponent>(facade.grass), Is.EqualTo(partition));
            Assert.That(world.Get<PBGltfContainer>(facade.environment).Src, Is.EqualTo("file1"));
            Assert.That(world.Get<PBGltfContainer>(facade.grass).Src, Is.EqualTo("file2"));
        }
    }
}
