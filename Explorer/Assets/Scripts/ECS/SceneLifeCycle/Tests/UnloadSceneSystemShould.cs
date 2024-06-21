using Arch.Core;
using DCL.LOD;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Linq;

namespace ECS.SceneLifeCycle.Tests
{
    public class UnloadSceneSystemShould : UnitySystemTestBase<UnloadSceneSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new UnloadSceneSystem(world, Substitute.For<IScenesCache>(), null);
        }

        [Test]
        public void DisposeLoadedScene()
        {
            ISceneFacade scene = Substitute.For<ISceneFacade>();
            Entity e = world.Create(scene, new DeleteEntityIntention(), new SceneDefinitionComponent());

            system.Update(0f);

            scene.Received(1).DisposeAsync();

            // remain scene definition
            Assert.That(world.GetArchetype(e).Types.Select(t => t.Type), Is.EquivalentTo(new[] { typeof(SceneDefinitionComponent) }));
        }
    }
}
