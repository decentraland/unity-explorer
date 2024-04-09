using Arch.Core;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;
using System.Threading.Tasks;

namespace ECS.SceneLifeCycle.Tests
{
    public class ControlSceneUpdateLoopSystemShould : UnitySystemTestBase<ControlSceneUpdateLoopSystem>
    {
        private IRealmPartitionSettings realmPartitionSettings;


        public void SetUp()
        {
            realmPartitionSettings = Substitute.For<IRealmPartitionSettings>();
            system = new ControlSceneUpdateLoopSystem(world, realmPartitionSettings, CancellationToken.None, Substitute.For<IScenesCache>());
        }


        public void StartScene()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            // Create resolve promise
            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));

            Entity e = world.Create(promise, PartitionComponent.TOP_PRIORITY);

            system.Update(0f);

            scene.Received(1).StartUpdateLoopAsync(Arg.Any<int>(), Arg.Any<CancellationToken>());
            Assert.That(world.Has<ISceneFacade>(e), Is.True);
        }


        public async Task StartSceneWithCorrectFPS()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            // Create resolve promise
            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention(), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));

            var partition = new PartitionComponent { Bucket = 3 };
            Entity e = world.Create(promise, partition);
            realmPartitionSettings.GetSceneUpdateFrequency(in partition).Returns(15);

            system.Update(0f);

            // let the system switch to the thread pool
            await Task.Delay(100);

            await scene.Received(1).StartUpdateLoopAsync(15, Arg.Any<CancellationToken>());
            Assert.That(world.Has<ISceneFacade>(e), Is.True);
        }


        public void ChangeSceneFPS()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            var partition = new PartitionComponent { Bucket = 3, IsDirty = true };
            realmPartitionSettings.GetSceneUpdateFrequency(in partition).Returns(15);

            world.Create(scene, partition, new SceneDefinitionComponent());

            system.Update(0f);

            scene.Received(1).SetTargetFPS(15);
        }
    }
}
