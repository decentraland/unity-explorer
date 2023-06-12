using Arch.Core;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using AssertionException = UnityEngine.Assertions.AssertionException;

namespace ECS.StreamableLoading.Tests
{
    public class AssetPromiseShould
    {
        public struct Intent : ILoadingIntention
        {
            public CommonLoadingArguments CommonArguments { get; set; }
        }

        public class Asset { }

        private World world;

        private AssetPromise<Asset, Intent> assetPromise;

        [SetUp]
        public void SetUp()
        {
            world = World.Create();

            assetPromise = AssetPromise<Asset, Intent>.Create(world, new Intent());
        }

        [Test]
        public void ProvideResultIfFinished()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryGetResult(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(asset, result.Asset);
            Assert.AreEqual(new StreamableLoadingResult<Asset>(asset), assetPromise.Result);
        }

        [Test]
        public void NotProvideResultIfNotFinished()
        {
            Assert.IsFalse(assetPromise.TryGetResult(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(default(StreamableLoadingResult<Asset>), result);
            Assert.AreEqual(null, assetPromise.Result);
        }

        [Test]
        public void ConsumeResult()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryConsume(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(asset, result.Asset);
            Assert.AreEqual(new StreamableLoadingResult<Asset>(asset), assetPromise.Result);

            Assert.IsFalse(assetPromise.Entity.IsAlive(world));
        }

        [Test]
        public void ForbidConsumingTwice()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryConsume(world, out _));
            Assert.Throws<AssertionException>(() => assetPromise.TryConsume(world, out _));
        }

        [Test]
        public void Forget()
        {
            assetPromise.ForgetLoading(world);

            Assert.IsTrue(assetPromise.LoadingIntention.CommonArguments.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(assetPromise.Entity.IsAlive(world));
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }
    }
}
