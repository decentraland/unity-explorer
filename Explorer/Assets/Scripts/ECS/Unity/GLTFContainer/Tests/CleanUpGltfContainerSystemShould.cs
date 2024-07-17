using Arch.Core;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Cache;
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
    public class CleanUpGltfContainerSystemShould : UnitySystemTestBase<CleanUpGltfContainerSystem>
    {
        private IGltfContainerAssetsCache cache;
        private IEntityCollidersSceneCache collidersSceneCache;

        [SetUp]
        public void SetUp()
        {
            cache = Substitute.For<IGltfContainerAssetsCache>();
            system = new CleanUpGltfContainerSystem(world, cache, collidersSceneCache = Substitute.For<IEntityCollidersSceneCache>());
        }

        [Test]
        public void Release()
        {
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1", "1_Hash", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            var asset = GltfContainerAsset.Create(new GameObject(), assetBundleReference: null);

            asset.DecodedVisibleSDKColliders = new List<SDKCollider> { new (), new () };
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity e = world.Create(c, new DeleteEntityIntention());

            system.Update(0);

            Assert.That(world.TryGet(e, out GltfContainerComponent component), Is.True);
            Assert.That(component.Promise.Entity, Is.EqualTo(EntityReference.Null));
            cache.Received(1).Dereference("1_Hash", Arg.Any<GltfContainerAsset>());
            collidersSceneCache.Received(2).Remove(Arg.Any<Collider>());
        }
    }
}
