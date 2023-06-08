using Arch.Core;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Tests
{
    public abstract class StartLoadingSystemBaseShould<TSystem, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: StartLoadingSystemBase<TIntention>
        where TIntention: struct, ILoadingIntention
    {
        protected abstract TIntention CreateIntention();

        protected abstract TSystem CreateSystem();

        private UnityWebRequest webRequest;

        [SetUp]
        public void BaseSetUp()
        {
            system = CreateSystem();
        }

        [TearDown]
        public void TearDown()
        {
            webRequest?.Dispose();
            webRequest = null;
        }

        private void StoreWebRequest(LoadingRequest loadingRequest)
        {
            webRequest = loadingRequest.WebRequest;

            if (webRequest != null)
            {
                webRequest.disposeDownloadHandlerOnDispose = true;
                webRequest.disposeUploadHandlerOnDispose = true;
            }
        }

        [Test]
        public void CreateNewRequest()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention);

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));

            StoreWebRequest(loadingRequest);

            Assert.IsNotNull(loadingRequest.WebRequest);
        }

        [Test]
        public void NotCreateRequestIfAborted()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention, new ForgetLoadingIntent());

            system.Update(0);

            Assert.IsFalse(world.Has<LoadingRequest>(e));
        }
    }
}
