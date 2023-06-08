using Arch.Core;
using ECS.StreamableLoading.Components.Common;
using ECS.StreamableLoading.Systems;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.Tests
{
    public abstract class RepeatLoadingSystemBaseShould<TSystem, TAsset, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: RepeatLoadingSystemBase<TIntention, TAsset>
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
        public void RepeatRequest()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention, new LoadingRequest());

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));

            StoreWebRequest(loadingRequest);

            Assert.IsNotNull(loadingRequest.WebRequest);
        }

        [Test]
        public void NotRepeatRequestIfResultIsSet()
        {
            TIntention intention = CreateIntention();
            Entity e = world.Create(intention, new LoadingRequest(), new StreamableLoadingResult<TAsset>());

            system.Update(0f);

            Assert.IsTrue(world.TryGet(e, out LoadingRequest loadingRequest));

            StoreWebRequest(loadingRequest);

            Assert.IsNull(loadingRequest.WebRequest);
        }
    }
}
