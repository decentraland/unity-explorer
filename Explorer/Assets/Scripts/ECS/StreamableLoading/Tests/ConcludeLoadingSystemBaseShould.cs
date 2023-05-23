using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.StreamableLoading.Components.Common;
using ECS.StreamableLoading.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using System.Threading.Tasks;

namespace ECS.StreamableLoading.Tests
{
    public abstract class ConcludeLoadingSystemBaseShould<TSystem, TAsset, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: ConcludeLoadingSystemBase<TAsset, TIntention>
        where TIntention: struct, ILoadingIntention
    {
        protected abstract TSystem CreateSystem();

        protected abstract Entity CreateSuccessIntention();

        protected abstract Entity CreateNotFoundIntention();

        protected abstract Entity CreateWrongTypeIntention();

        [SetUp]
        public void BaseSetUp()
        {
            system = CreateSystem();
        }

        [Test]
        public async Task ConcludeSuccess()
        {
            Entity e = CreateSuccessIntention();

            LoadingRequest request = world.Get<LoadingRequest>(e);

            while (!request.WebRequest.isDone)
            {
                system.Update(0);
                await UniTask.Yield();
            }

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out StreamableLoadingResult<TAsset> result));
            Assert.IsNotNull(result.Asset);
            Assert.IsTrue(result.Succeeded);
        }

        [Test]
        public async Task ConcludeExceptionOnParseFail()
        {
            Entity e = CreateWrongTypeIntention();

            LoadingRequest request = world.Get<LoadingRequest>(e);

            while (!request.WebRequest.isDone)
            {
                system.Update(0);
                await UniTask.Yield();
            }

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out StreamableLoadingResult<TAsset> result));
            Assert.IsNull(result.Asset);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Exception);
        }

        [Test]
        public async Task ConcludeFailIfNotFound()
        {
            Entity e = CreateNotFoundIntention();

            void FixIntention()
            {
                ref TIntention intention = ref world.Get<TIntention>(e);
                CommonLoadingArguments ca = intention.CommonArguments;
                ca.Attempts = 1;
                intention.CommonArguments = ca;
            }

            FixIntention();

            LoadingRequest request = world.Get<LoadingRequest>(e);

            while (!request.WebRequest.isDone)
            {
                system.Update(0);
                await UniTask.Yield();
            }

            system.Update(0);

            Assert.IsTrue(world.TryGet(e, out StreamableLoadingResult<TAsset> result));
            Assert.IsNull(result.Asset);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Exception);
        }
    }
}
