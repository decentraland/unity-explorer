using Arch.Core;
using DCL.ECSComponents;
using DCL.Utilities;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using ECS.Unity.Tween.Systems;
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
            system = new TweenLoaderSystem(world);

            entity = world.Create(PartitionComponent.TOP_PRIORITY);
            AddTransformToEntity(entity);
        }

        private Entity entity;
        private World globalWorld;

    }
}
