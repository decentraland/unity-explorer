using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.GLTF;
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
using Utility.Primitives;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class ResetGltfContainerSystemShould : UnitySystemTestBase<ResetGltfContainerSystem>
    {
        private IGltfContainerAssetsCache cache;
        private IEntityCollidersSceneCache entityCollidersSceneCache;
        private EntityEventBuffer<GltfContainerComponent> eventBuffer;
        private IECSToCRDTWriter ecsToCRDTWriter;

        [SetUp]
        public void SetUp()
        {
            system = new ResetGltfContainerSystem(world,
                cache = Substitute.For<IGltfContainerAssetsCache>(),
                entityCollidersSceneCache = Substitute.For<IEntityCollidersSceneCache>(),
                eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1),
                ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>());
        }

        [Test]
        public void InvalidatePromiseIfSourceChanged()
        {
            var sdkComponent = new PBGltfContainer { IsDirty = true, Src = "2" };
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1","1_Hash", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            var asset = GltfContainerAsset.Create(new GameObject(), assetData: null);
            asset.DecodedVisibleSDKColliders = new List<SDKCollider> { new () };
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity entity = world.Create(sdkComponent, c);

            system.Update(0);

            Assert.That(world.TryGet(entity, out GltfContainerComponent component), Is.True);
            Assert.That(component.State, Is.EqualTo(LoadingState.Unknown));
            Assert.That(eventBuffer.Relations, Contains.Item(new EntityRelation<GltfContainerComponent>(entity, component)));
            Assert.That(component.Promise, Is.EqualTo(AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL));

            cache.Received(1).Dereference("1_Hash", Arg.Any<GltfContainerAsset>());
            entityCollidersSceneCache.Received(1).Remove(Arg.Any<Collider>());
        }

        [Test]
        public void ReleaseIfComponentRemoved()
        {
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("1","1_Hash", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);

            var asset = GltfContainerAsset.Create(new GameObject(), assetData: null);
            asset.DecodedVisibleSDKColliders = new List<SDKCollider> { new () };

            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity entity = world.Create(c, new CRDTEntity(100));

            system.Update(0);

            Assert.That(world.Has<GltfContainerComponent>(entity), Is.False);
            Assert.That(c.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            entityCollidersSceneCache.Received(1).Remove(Arg.Any<Collider>());
            ecsToCRDTWriter.Received().DeleteMessage<PBGltfContainerLoadingState>(new CRDTEntity(100));
        }

        [Test]
        public void ForgetInFlightLoadingOnSrcChange()
        {
            var sdkComponent = new PBGltfContainer { IsDirty = true, Src = "new-src" };
            var cts = new CancellationTokenSource();
            var c = new GltfContainerComponent
            {
                Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("old-src", "old_hash", cts), PartitionComponent.TOP_PRIORITY),
                State = LoadingState.Loading,
            };

            // No StreamableLoadingResult added — the promise is still in-flight
            Entity loadingEntity = c.Promise.Entity;
            Assume.That(world.IsAlive(loadingEntity), Is.True);
            Assume.That(cts.IsCancellationRequested, Is.False);

            Entity entity = world.Create(sdkComponent, c);

            system.Update(0);

            // Loading entity is destroyed so it cannot resolve into an orphaned GltfContainerAsset later
            Assert.That(world.IsAlive(loadingEntity), Is.False);
            Assert.That(cts.IsCancellationRequested, Is.True);

            Assert.That(world.TryGet(entity, out GltfContainerComponent updated), Is.True);
            Assert.That(updated.Promise, Is.EqualTo(AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.NULL));
            Assert.That(updated.State, Is.EqualTo(LoadingState.Unknown));
        }

        [Test]
        public void DisposeRawGltfOnComponentRemovalInsteadOfCaching()
        {
            var c = new GltfContainerComponent();
            c.Promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("raw-src", "raw_hash", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);

            // Simulate a raw GLTF that LoadGLTFSystem reference-counted and handed to GltfContainerAsset
            var gltfData = new GLTFData(null!, new GameObject("RawGLTF-Root"));
            gltfData.AddReference();

            var asset = GltfContainerAsset.Create(new GameObject("container"), assetData: gltfData);
            world.Add(c.Promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            c.State = LoadingState.Finished;

            Entity entity = world.Create(c, new CRDTEntity(100));

            system.Update(0);

            Assert.That(world.Has<GltfContainerComponent>(entity), Is.False);

            // Raw GLTFs must not be returned to the shared pool — the cache is irrelevant for NoCache assets in LSD
            cache.DidNotReceive().Dereference(Arg.Any<string>(), Arg.Any<GltfContainerAsset>());

            // GLTFData was dereferenced and disposed through GltfContainerAsset.Dispose
            Assert.That(gltfData.CanBeDisposed(), Is.True, "GLTFData.RefCount should have been decremented by the container's Dispose");
            Assert.That(asset.AssetData, Is.Null, "GltfContainerAsset.Dispose should null out AssetData");
        }

        [Test]
        public void ReleaseContainerWhenEntityHasGltfNodeModifiers()
        {
            // Arrange
            var originalMaterial = new Material(DefaultMaterial.Get());
            var newMaterial = new Material(DefaultMaterial.Get());
            var testGameObject = new GameObject("TestRenderer");
            var meshRenderer = testGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = newMaterial;

            var rootGameObject = new GameObject();
            var asset = GltfContainerAsset.Create(rootGameObject, null);
            asset.Renderers.Add(meshRenderer);

            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention("test", "test_hash", new CancellationTokenSource()), PartitionComponent.TOP_PRIORITY);
            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            var gltfContainerComponent = new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
            };

            // Add the GltfNodeModifiers component to simulate an entity with node modifiers
            var entity = world.Create(gltfContainerComponent, new CRDTEntity(100), new GltfNodeModifiers.Components.GltfNodeModifiers(new Dictionary<Entity, string>(), new Dictionary<Renderer, Material>()));

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfContainerComponent>(entity), Is.False);
            Assert.That(gltfContainerComponent.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);

            // Cleanup
            Object.DestroyImmediate(testGameObject);
            Object.DestroyImmediate(rootGameObject);
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(newMaterial);
        }
    }
}
