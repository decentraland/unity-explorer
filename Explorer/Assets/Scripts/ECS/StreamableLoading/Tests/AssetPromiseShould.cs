using Arch.Core;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using NUnit.Framework;
using System;
using System.Threading;
using AssertionException = UnityEngine.Assertions.AssertionException;

namespace ECS.StreamableLoading.Tests
{
    public class AssetPromiseShould
    {
        private World world;

        private AssetPromise<Asset, Intent> assetPromise;


        public void SetUp()
        {
            world = World.Create();

            assetPromise = AssetPromise<Asset, Intent>.Create(world, new Intent { CommonArguments = new CommonLoadingArguments("URL") }, PartitionComponent.TOP_PRIORITY);
        }


        public void ProvideResultIfFinished()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryGetResult(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(asset, result.Asset);
            Assert.AreEqual(new StreamableLoadingResult<Asset>(asset), assetPromise.Result);
        }


        public void NotProvideResultIfNotFinished()
        {
            Assert.IsFalse(assetPromise.TryGetResult(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(default(StreamableLoadingResult<Asset>), result);
            Assert.AreEqual(null, assetPromise.Result);
        }


        public void ConsumeResult()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryConsume(world, out StreamableLoadingResult<Asset> result));
            Assert.AreEqual(asset, result.Asset);
            Assert.AreEqual(new StreamableLoadingResult<Asset>(asset), assetPromise.Result);

            Assert.IsFalse(assetPromise.Entity.IsAlive(world));
        }


        public void ForbidConsumingTwice()
        {
            var asset = new Asset();

            world.Add(assetPromise.Entity.Entity, new StreamableLoadingResult<Asset>(asset));

            Assert.IsTrue(assetPromise.TryConsume(world, out _));
            Assert.Throws<AssertionException>(() => assetPromise.TryConsume(world, out _));
        }


        public void Forget()
        {
            assetPromise.ForgetLoading(world);

            Assert.IsTrue(assetPromise.LoadingIntention.CommonArguments.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(assetPromise.Entity.IsAlive(world));
        }


        public void TearDown()
        {
            world.Dispose();
        }

        public struct Intent : ILoadingIntention, IEquatable<Intent>
        {
            public CommonLoadingArguments CommonArguments { get; set; }
            public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

            public bool Equals(Intent other) =>
                this.AreUrlEquals(other);
        }

        public class Asset { }
    }
}
