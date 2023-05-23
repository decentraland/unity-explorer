using Arch.Core;
using ECS.StreamableLoading.Components.Common;
using ECS.StreamableLoading.Systems;
using ECS.TestSuite;
using NUnit.Framework;

namespace ECS.StreamableLoading.Tests
{
    public abstract class StartLoadingSystemBaseShould<TSystem, TAsset, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: StartLoadingSystemBase<TIntention, TAsset>
        where TIntention: struct, ILoadingIntention
    {
        protected abstract TIntention CreateIntention();

        protected abstract TSystem CreateSystem();

        [SetUp]
        public void BaseSetUp()
        {
            system = CreateSystem();
        }

        [Test]
        public void CreateNewRequest()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention);

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));
            Assert.IsNotNull(loadingRequest.WebRequest);
        }

        [Test]
        public void RepeatRequest()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention, new LoadingRequest());

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));
            Assert.IsNotNull(loadingRequest.WebRequest);
        }

        [Test]
        public void NotRepeatRequestIfResultIsSet()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention, new LoadingRequest(), new StreamableLoadingResult<TAsset>());

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));
            Assert.IsNull(loadingRequest.WebRequest);
        }
    }
}
