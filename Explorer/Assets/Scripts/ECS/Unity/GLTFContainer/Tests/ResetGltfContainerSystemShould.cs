﻿using Arch.Core;
using DCL.ECSComponents;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class ResetGltfContainerSystemShould : UnitySystemTestBase<ResetGltfContainerSystem>
    {
        private IStreamableCache<GltfContainerAsset, string> cache;

        [SetUp]
        public void SetUp()
        {
            system = new ResetGltfContainerSystem(world, cache = Substitute.For<IStreamableCache<GltfContainerAsset, string>>());
        }

        [Test]
        public void InvalidatePromiseIfSourceChanged()
        {
            var sdkComponent = new PBGltfContainer { IsDirty = true, Src = "2" };
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1", new CancellationTokenSource()));
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(GltfContainerAsset.Create(new GameObject())));
            c.State.Set(LoadingState.Finished);

            Entity entity = world.Create(sdkComponent, c);

            system.Update(0);

            Assert.That(world.TryGet(entity, out GltfContainerComponent component), Is.True);
            Assert.That(component.State.Value, Is.EqualTo(LoadingState.Unknown));
            Assert.That(component.Promise, Is.EqualTo(AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL));

            cache.Received(1).Dereference("1", Arg.Any<GltfContainerAsset>());
        }

        [Test]
        public void ReleaseIfComponentRemoved()
        {
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1", new CancellationTokenSource()));
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(GltfContainerAsset.Create(new GameObject())));
            c.State.Set(LoadingState.Finished);

            Entity entity = world.Create(c);

            system.Update(0);

            Assert.That(world.Has<GltfContainerComponent>(entity), Is.False);
            Assert.That(c.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
        }
    }
}
