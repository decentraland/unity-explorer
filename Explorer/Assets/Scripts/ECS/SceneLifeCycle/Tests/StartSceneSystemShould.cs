using Arch.Core;
using ECS.SceneLifeCycle.Systems;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Threading;

namespace ECS.SceneLifeCycle.Tests
{
    public class StartSceneSystemShould : UnitySystemTestBase<StartSceneSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new StartSceneSystem(world, CancellationToken.None);
        }

        [Test]
        public void StartScene()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();

            // Create resolve promise
            var promise = AssetPromise<ISceneFacade, GetSceneFacadeIntention>.Create(world, new GetSceneFacadeIntention());
            world.Add(promise.Entity, new StreamableLoadingResult<ISceneFacade>(scene));

            Entity e = world.Create(promise);

            system.Update(0f);

            scene.Received(1).StartUpdateLoop(Arg.Any<int>(), Arg.Any<CancellationToken>());
            Assert.That(world.Has<ISceneFacade>(e), Is.True);
        }
    }
}
