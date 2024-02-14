using Arch.Core;
using DCL.SDKComponents.Tween.Systems;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using NUnit.Framework;

namespace ECS.Unity.Tween.Tests
{
    [TestFixture]
    public class TweenLoaderSystemShould : UnitySystemTestBase<TweenLoaderSystem>
    {
        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            var worldProxy = new WorldProxy();
            worldProxy.SetWorld(globalWorld);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        private Entity entity;
        private World globalWorld;

    }
}
