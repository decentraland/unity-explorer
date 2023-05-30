using Arch.Core;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using NSubstitute;
using NUnit.Framework;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class CleanUpMaterialsSystemShould : UnitySystemTestBase<CleanUpMaterialsSystem>
    {
        private IMaterialsCache materialsCache;

        private Entity e;
        private Entity texPromise;

        [SetUp]
        public void SetUp()
        {
            system = new CleanUpMaterialsSystem(world, materialsCache = Substitute.For<IMaterialsCache>());

            e = world.Create(new MaterialComponent { AlbedoTexPromise = world.Reference(texPromise = world.Create()) }, new DeleteEntityIntention());
        }

        [Test]
        public void AbortLoadingIntentions()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            system.Update(0);

            Assert.That(world.Has<ForgetLoadingIntent>(texPromise), Is.True);
            Assert.That(world.Get<MaterialComponent>(e).AlbedoTexPromise, Is.EqualTo(EntityReference.Null));
        }

        [Test]
        public void Dereference()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = MaterialComponent.LifeCycle.LoadingFinished;
            component.Result = DefaultMaterial.New();

            system.Update(0);

            materialsCache.Received(1).Dereference(in component.Data);
            Assert.That(world.Get<MaterialComponent>(e).Result, Is.Null);
        }
    }
}
