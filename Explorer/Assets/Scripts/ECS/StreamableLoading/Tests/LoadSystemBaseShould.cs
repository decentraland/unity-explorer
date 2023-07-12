using AssetManagement;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene.Tests;
using System;
using System.Threading.Tasks;

namespace ECS.StreamableLoading.Tests
{
    public abstract class LoadSystemBaseShould<TSystem, TAsset, TIntention> : UnitySystemTestBase<TSystem>
        where TSystem: LoadSystemBase<TAsset, TIntention>
        where TIntention: struct, ILoadingIntention, IEquatable<TIntention>
    {
        protected abstract TIntention CreateSuccessIntention();

        protected abstract TIntention CreateNotFoundIntention();

        protected abstract TIntention CreateWrongTypeIntention();

        protected abstract TSystem CreateSystem();

        protected AssetPromise<TAsset, TIntention> promise { get; private set; }

        protected IStreamableCache<TAsset, TIntention> cache;

        private MockedReportScope mockedReportScope;

        [SetUp]
        public void BaseSetUp()
        {
            mockedReportScope = new MockedReportScope();

            cache = Substitute.For<IStreamableCache<TAsset, TIntention>>();
            system = CreateSystem();
            system.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            mockedReportScope.Dispose();
            promise.LoadingIntention.CommonArguments.CancellationTokenSource?.Cancel();
        }

        [Test]
        public async Task ConcludeSuccess()
        {
            promise = AssetPromise<TAsset, TIntention>.Create(world, CreateSuccessIntention());

            // Launch the flow
            system.Update(0);

            promise = await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);

            Assert.That(promise.TryGetResult(world, out StreamableLoadingResult<TAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Asset, Is.Not.Null);

            AssertSuccess(result.Asset);
        }

        [Test]
        public async Task ConcludeExceptionOnParseFail()
        {
            promise = AssetPromise<TAsset, TIntention>.Create(world, CreateWrongTypeIntention());

            // Launch the flow
            system.Update(0);

            promise = await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);

            Assert.That(promise.TryGetResult(world, out StreamableLoadingResult<TAsset> result), Is.True);
            Assert.That(result.Succeeded, Is.False);
            Assert.IsNotNull(result.Exception);
        }

        [Test]
        public async Task ConcludeFailIfNotFound()
        {
            TIntention intent = CreateNotFoundIntention();
            intent.SetAttempts(1);

            promise = AssetPromise<TAsset, TIntention>.Create(world, intent);

            // Launch the flow
            system.Update(0);

            promise = await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);

            Assert.That(promise.TryGetResult(world, out StreamableLoadingResult<TAsset> result), Is.True);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNotNull(result.Exception);
        }

        [Test]
        public async Task RemoveCurrentSourceFromPermittedSources()
        {
            TIntention intent = CreateSuccessIntention();
            intent.SetSources(AssetSource.EMBEDDED, AssetSource.EMBEDDED);

            promise = AssetPromise<TAsset, TIntention>.Create(world, CreateSuccessIntention());

            // Launch the flow
            system.Update(0);

            Assert.AreEqual(AssetSource.NONE, world.Get<TIntention>(promise.Entity).CommonArguments.PermittedSources);
            await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);
        }

        [Test]
        public async Task GetAssetFromCache()
        {
            TIntention successIntent = CreateSuccessIntention();
            promise = AssetPromise<TAsset, TIntention>.Create(world, successIntent);

            TIntention checkIntent = successIntent;
            checkIntent.RemoveCurrentSource();

            // Launch the flow
            system.Update(0);

            cache.Received(1).TryGet(checkIntent, out Arg.Any<TAsset>());
            cache.ClearReceivedCalls();

            promise = await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);
            Assert.That(promise.TryGetResult(world, out StreamableLoadingResult<TAsset> result), Is.True);

            // Second time

            cache.TryGet(in checkIntent, out Arg.Any<TAsset>())
                 .Returns(c =>
                  {
                      c[1] = result.Asset;
                      return true;
                  });

            promise = AssetPromise<TAsset, TIntention>.Create(world, successIntent);

            // Launch the flow
            system.Update(0);

            // should exit immediately
            Assert.That(world.Has<LoadingInProgress>(promise.Entity), Is.False);

            promise = await promise.ToUniTask(world, cancellationToken: promise.LoadingIntention.CommonArguments.CancellationToken);

            Assert.That(promise.TryGetResult(world, out result), Is.True);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Asset, Is.Not.Null);
        }

        /// <summary>
        ///     Additional assertion for successful promise
        /// </summary>
        /// <param name="asset"></param>
        protected virtual void AssertSuccess(TAsset asset) { }
    }
}
