using Arch.Core;
using DCL.Ipfs;
using DCL.Utilities;
using ECS;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.Reporting;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utility;

namespace DCL.SceneLifeCycle.Tests
{
    public class ControlSceneUpdateLoopSystemShould : UnitySystemTestBase<ControlSceneUpdateLoopSystem>
    {
        private IRealmPartitionSettings realmPartitionSettings = null!;
        private ISceneReadinessReportQueue sceneReadinessReportQueue = null!;
        private IRealmData realmData = null!;
        private ISceneRoomStatus sceneRoomStatus = null!;

        [SetUp]
        public void SetUp()
        {
            realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();
            realmData = Substitute.For<IRealmData>();
            sceneRoomStatus = Substitute.For<ISceneRoomStatus>();

            system = new ControlSceneUpdateLoopSystem(world, realmPartitionSettings, CancellationToken.None, Substitute.For<IScenesCache>(), sceneReadinessReportQueue,
                realmData, sceneRoomStatus);
        }

        [Test]
        public void StartScene()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            // Create resolve promise
            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention(), PartitionComponent.TOP_PRIORITY);

            SceneDefinitionComponent sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { Vector3.zero.ToParcel() } },
                },
            }, new IpfsPath());
            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));

            Entity e = world.Create(promise, PartitionComponent.TOP_PRIORITY, sceneDefinitionComponent);

            system?.Update(0f);

            scene.Received(1).StartUpdateLoopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            Assert.That(world.Has<ISceneFacade>(e), Is.True);
        }

        [Test]
        public async Task StartSceneWithCorrectFPS()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            // Create resolve promise
            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention(), PartitionComponent.TOP_PRIORITY);

            SceneDefinitionComponent sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(new SceneEntityDefinition
            {
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { Vector3.zero.ToParcel() } },
                },
            }, new IpfsPath());
            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));

            var partition = new PartitionComponent { Bucket = 3 };
            Entity e = world.Create(promise, partition, sceneDefinitionComponent);
            realmPartitionSettings.GetSceneUpdateFrequency(in partition).Returns(15);

            system?.Update(0f);

            // let the system switch to the thread pool
            await Task.Delay(100);

            await scene.Received(1).StartUpdateLoopAsync(15, Arg.Any<CancellationToken>());
            Assert.That(world.Has<ISceneFacade>(e), Is.True);
        }

        [Test]
        public void HoldWorldSceneStartUntilSceneRoomIsSettled()
        {
            ISceneFacade scene = CreateWorldScenePendingStart(isRoomSettled: false, hasReadinessReport: true);

            system?.Update(0f);

            scene.DidNotReceive().StartUpdateLoopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void StartWorldSceneWhenSceneRoomIsSettled()
        {
            ISceneFacade scene = CreateWorldScenePendingStart(isRoomSettled: true, hasReadinessReport: true);

            system?.Update(0f);

            scene.Received(1).StartUpdateLoopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void StartWorldSceneWithoutReadinessReportRegardlessOfSceneRoom()
        {
            ISceneFacade scene = CreateWorldScenePendingStart(isRoomSettled: false, hasReadinessReport: false);

            system?.Update(0f);

            scene.Received(1).StartUpdateLoopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
        }

        [Test]
        public void ChangeSceneFPS()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            var partition = new PartitionComponent { Bucket = 3, IsDirty = true };
            realmPartitionSettings.GetSceneUpdateFrequency(in partition).Returns(15);

            world.Create(scene, partition, new SceneDefinitionComponent());

            system?.Update(0f);

            scene.Received(1).SetTargetFPS(15);
        }

        private ISceneFacade CreateWorldScenePendingStart(bool isRoomSettled, bool hasReadinessReport)
        {
            realmData.Configured.Returns(true);
            realmData.RealmType.Returns(new ReactiveProperty<RealmKind>(RealmKind.World));
            sceneReadinessReportQueue.HasReport(Arg.Any<IReadOnlyList<Vector2Int>>()).Returns(hasReadinessReport);
            sceneRoomStatus.IsSceneRoomSettled(Arg.Any<string>()).Returns(isRoomSettled);

            ISceneFacade scene = Substitute.For<ISceneFacade>();

            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention(), PartitionComponent.TOP_PRIORITY);

            SceneDefinitionComponent sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(new SceneEntityDefinition
            {
                id = "test-scene",
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                        { DecodedParcels = new[] { Vector3.zero.ToParcel() } },
                },
            }, new IpfsPath());

            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));
            world.Create(promise, PartitionComponent.TOP_PRIORITY, sceneDefinitionComponent);

            return scene;
        }
    }
}
