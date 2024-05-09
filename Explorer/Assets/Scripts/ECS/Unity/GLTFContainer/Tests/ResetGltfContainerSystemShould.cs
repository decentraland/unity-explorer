using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using ECS.Unity.SceneBoundsChecker;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class ResetGltfContainerSystemShould : UnitySystemTestBase<ResetGltfContainerSystem>
    {
        private IDereferencableCache<GltfContainerAsset, string> cache;
        private IEntityCollidersSceneCache entityCollidersSceneCache;
        private EntityEventBuffer<GltfContainerComponent> eventBuffer;

        [SetUp]
        public void SetUp()
        {
            system = new ResetGltfContainerSystem(world,
                cache = Substitute.For<IDereferencableCache<GltfContainerAsset, string>>(),
                entityCollidersSceneCache = Substitute.For<IEntityCollidersSceneCache>(),
                eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1));
        }

        [Test]
        public void InvalidatePromiseIfSourceChanged()
        {
            var sdkComponent = new PBGltfContainer { IsDirty = true, Src = "2" };
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            var asset = GltfContainerAsset.Create(new GameObject(), null);
            asset.VisibleMeshesColliders = new List<SDKCollider> { new () };
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity entity = world.Create(sdkComponent, c);

            system.Update(0);

            Assert.That(world.TryGet(entity, out GltfContainerComponent component), Is.True);
            Assert.That(component.State, Is.EqualTo(LoadingState.Unknown));
            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(entity, component)));
            Assert.That(component.Promise, Is.EqualTo(AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL));

            cache.Received(1).Dereference("1", Arg.Any<GltfContainerAsset>());
            entityCollidersSceneCache.Received(1).Remove(Arg.Any<Collider>());
        }

        [Test]
        public void ReleaseIfComponentRemoved()
        {
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);

            var asset = GltfContainerAsset.Create(new GameObject(), null);
            asset.VisibleMeshesColliders = new List<SDKCollider> { new () };

            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity entity = world.Create(c);

            system.Update(0);

            Assert.That(world.Has<GltfContainerComponent>(entity), Is.False);
            Assert.That(c.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            entityCollidersSceneCache.Received(1).Remove(Arg.Any<Collider>());
        }
    }
}
