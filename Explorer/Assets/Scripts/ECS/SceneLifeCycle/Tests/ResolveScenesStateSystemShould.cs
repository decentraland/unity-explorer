using Arch.Core;
using CrdtEcsBridge.Components.Special;
using ECS.SceneLifeCycle.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace ECS.SceneLifeCycle.Tests
{
    public class ResolveScenesStateSystemShould : UnitySystemTestBase<ResolveScenesStateSystem>
    {
        private SceneLifeCycleState state;

        [SetUp]
        public void SetUp()
        {
            Entity playerEntity = world.Create(new PlayerComponent());
            AddTransformToEntity(playerEntity);

            system = new ResolveScenesStateSystem(world, state = new SceneLifeCycleState
            {
                SceneLoadRadius = 2,
                PlayerEntity = playerEntity,
            });
        }

        [Test]
        public void WaitForAssetBundleManifestResolution()
        {
            // TODO
        }
    }
}
