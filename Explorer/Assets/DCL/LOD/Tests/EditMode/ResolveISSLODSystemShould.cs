using Arch.Core;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SceneRunner.Scene;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.AssetLoad.Cache;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Asset.Tests;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TestTools;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace DCL.LOD.Tests
{
    /// <summary>
    ///     Ref-count and bridge-slot balance tests for <see cref="ResolveISSLODSystem" />.
    ///     The system's two queries are the ones touching ref counts:
    ///     - cache-hit path calls <c>gltfCache.TryGet</c> + <c>TryReleaseBridgeSlot</c>
    ///     - <c>ConvertFromAssetBundle</c>'s "not still relevant" branch calls <c>Result.Asset.Dereference()</c>
    ///     If either is mispaired the asset leaks for the rest of the session.
    /// </summary>
    [TestFixture]
    public class ResolveISSLODSystemShould : UnitySystemTestBase<ResolveISSLODSystem>
    {
        private const string SCENE_ID = "FAKE_ISS_SCENE";

        private static GltfContainerTestResources sharedResources;
        private static StreamableLoadingResult<AssetBundleData> sharedAB;

        private TrackingGltfCache cache;
        private SceneDefinitionComponent sceneDefinition;

        [SetUp]
        public void SetUp()
        {
            cache = new TrackingGltfCache();

            IPerformanceBudget budget = Substitute.For<IPerformanceBudget>();
            budget.TrySpendBudget().Returns(true);

            system = new ResolveISSLODSystem(world, cache, budget, budget);

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = SCENE_ID,
                metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0),
                        DecodedParcels = new[] { new Vector2Int(0, 0) },
                    },
                },
            };

            sceneDefinition = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            sharedAB.Asset?.Dispose(true);
            sharedResources?.UnloadBundle();
            sharedResources = null;
            sharedAB = default(StreamableLoadingResult<AssetBundleData>);
        }

        private static async Task<StreamableLoadingResult<AssetBundleData>> EnsureSharedAB()
        {
            if (sharedAB.Asset != null) return sharedAB;
            sharedResources = new GltfContainerTestResources();
            sharedAB = await sharedResources.LoadAssetBundle(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH);
            return sharedAB;
        }

        [Test]
        public void DereferenceEveryCacheHitOnDispose()
        {
            const string HASH_A = "ITEM_A";
            const string HASH_B = "ITEM_B";

            cache.Stash(HASH_A, MakeFakeGltf(HASH_A));
            cache.Stash(HASH_B, MakeFakeGltf(HASH_B));

            var descriptor = ISSDescriptor.CreateUninitialized();

            descriptor.MarkResolved(new[]
            {
                NewDescriptorEntry(HASH_A),
                NewDescriptorEntry(HASH_B),
            });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);

            system.Update(0);

            Assert.That(lod.AllAssetsInstantiated(), Is.True);
            Assert.That(cache.Outstanding(HASH_A), Is.EqualTo(1));
            Assert.That(cache.Outstanding(HASH_B), Is.EqualTo(1));

            lod.Dispose(world);

            Assert.That(cache.Outstanding(HASH_A), Is.EqualTo(0), "Asset A must be returned to the pool");
            Assert.That(cache.Outstanding(HASH_B), Is.EqualTo(0), "Asset B must be returned to the pool");
        }

        [Test]
        public async Task DereferenceAssetBundleOnAbortedRun()
        {
            StreamableLoadingResult<AssetBundleData> abResult = await EnsureSharedAB();

            // Mimic the loader: each delivered result holds one reference.
            abResult.Asset.AddReference();
            int refsBefore = abResult.Asset.referenceCount;

            var descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(new[] { NewDescriptorEntry(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH) });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);

            system.Update(0);

            Entity helper = FindHelperEntity();
            Assert.That(helper, Is.Not.EqualTo(Entity.Null), "Update must spawn one helper entity per descriptor asset");

            // Abort the run before the result arrives — Generation bump makes the still-pending result irrelevant.
            lod.ForgetLoading(world);

            DeliverResult(helper, abResult);
            system.Update(0);

            Assert.That(abResult.Asset.referenceCount, Is.EqualTo(refsBefore - 1),
                "ConvertFromAssetBundle's cancellation branch must dereference the AB exactly once");
        }

        [Test]
        public async Task KeepAssetBundleReferencedOnSuccessfulInstantiation()
        {
            StreamableLoadingResult<AssetBundleData> abResult = await EnsureSharedAB();

            abResult.Asset.AddReference();
            int refsBefore = abResult.Asset.referenceCount;

            var descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(new[] { NewDescriptorEntry(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH) });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);

            system.Update(0);
            DeliverResult(FindHelperEntity(), abResult);
            system.Update(0);

            Assert.That(lod.AllAssetsInstantiated(), Is.True);

            Assert.That(abResult.Asset.referenceCount, Is.EqualTo(refsBefore),
                "Success path must not dereference the AB — GltfContainerAsset.Dispose owns that on later eviction");

            Assert.That(cache.DereferenceCalls(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH), Is.EqualTo(0));

            lod.Dispose(world);

            Assert.That(cache.DereferenceCalls(GltfContainerTestResources.RENDERER_WITH_LEGACY_ANIM_HASH), Is.EqualTo(1),
                "lod.Dispose must hand the instantiated asset back to the gltf cache exactly once");
        }

        [Test]
        public void RecordFailedAssetWithoutTouchingCache()
        {
            LogAssert.ignoreFailingMessages = true;

            var descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(new[] { NewDescriptorEntry("MISSING_HASH") });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);
            system.Update(0);

            var failure = new StreamableLoadingResult<AssetBundleData>(
                ReportData.UNSPECIFIED,
                new StreamableLoadingException(LogType.Warning, "simulated"));

            DeliverResult(FindHelperEntity(), failure);
            system.Update(0);

            Assert.That(lod.AllAssetsInstantiated(), Is.True,
                "AddFailedAsset must let AllAssetsInstantiated settle so UnloadLODForISS can bridge any successes");

            Assert.That(cache.Outstanding("MISSING_HASH"), Is.EqualTo(0));
        }

        [Test]
        public void SuppressRenderingButKeepObjectsActiveUntilRevealed()
        {
            // Guards the LOD_1 -> LOD_0 atomic-swap fix: while descriptor assets stream in, their rendering
            // must be suppressed so the half-assembled LOD_0 never draws on top of the still-visible LOD_1.
            // Crucially the GameObjects stay active so colliders remain registered; the reveal (owned by
            // InstantiateSceneLODInfoSystem at the swap) restores rendering, not GameObject activation.
            const string HASH_A = "ITEM_A";
            const string HASH_B = "ITEM_B";

            GltfContainerAsset assetA = MakeFakeGltfWithRenderer(HASH_A, out Renderer rendererA);
            GltfContainerAsset assetB = MakeFakeGltfWithRenderer(HASH_B, out Renderer rendererB);
            cache.Stash(HASH_A, assetA);
            cache.Stash(HASH_B, assetB);

            var descriptor = ISSDescriptor.CreateUninitialized();

            descriptor.MarkResolved(new[]
            {
                NewDescriptorEntry(HASH_A),
                NewDescriptorEntry(HASH_B),
            });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);

            system.Update(0);

            Assert.That(lod.AllAssetsInstantiated(), Is.True);

            Assert.That(rendererA.forceRenderingOff, Is.True, "Rendering must be suppressed while LOD_0 assembles");
            Assert.That(rendererB.forceRenderingOff, Is.True, "Rendering must be suppressed while LOD_0 assembles");
            Assert.That(assetA.Root.activeInHierarchy, Is.True, "GameObject must stay active so colliders survive");
            Assert.That(assetB.Root.activeInHierarchy, Is.True, "GameObject must stay active so colliders survive");

            lod.RevealAssembledAssets();

            Assert.That(rendererA.forceRenderingOff, Is.False, "Reveal must hand rendering back so the LODGroup can cull by distance");
            Assert.That(rendererB.forceRenderingOff, Is.False, "Reveal must hand rendering back so the LODGroup can cull by distance");

            lod.Dispose(world);
        }

        [Test]
        public void RestoreRenderingWhenAbortedBeforeReveal()
        {
            // An aborted run (ForgetLoading while PROCESSING) dereferences positioned assets back to the
            // cache. They must not return with forceRenderingOff stuck on, or they reappear invisible on reuse.
            const string HASH = "ITEM_A";

            GltfContainerAsset asset = MakeFakeGltfWithRenderer(HASH, out Renderer renderer);
            cache.Stash(HASH, asset);

            var descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(new[] { NewDescriptorEntry(HASH) });

            InitialSceneStateLOD lod = CreateLODEntity(descriptor);

            system.Update(0);
            Assert.That(renderer.forceRenderingOff, Is.True);

            lod.Dispose(world);

            Assert.That(renderer.forceRenderingOff, Is.False,
                "Clear must restore rendering before the asset is handed back to the cache");
        }

        [Test]
        public void ReleaseBridgeSlotOnCacheHit()
        {
            const string HASH = "BRIDGE_HASH";

            cache.Stash(HASH, MakeFakeGltf(HASH));

            var descriptor = ISSDescriptor.CreateUninitialized();
            descriptor.MarkResolved(new[] { NewDescriptorEntry(HASH) });

            // Reserve the only slot (capacity == 1) to mimic an SDK→LOD bridge handoff.
            Assert.That(descriptor.TryReserveBridgeSlot(HASH), Is.True);
            Assert.That(descriptor.TryReserveBridgeSlot(HASH), Is.False, "Capacity is 1 — second reserve must fail");

            CreateLODEntity(descriptor);
            system.Update(0);

            Assert.That(descriptor.TryReserveBridgeSlot(HASH), Is.True,
                "Cache hit must call TryReleaseBridgeSlot so the slot is reusable");
        }

        private InitialSceneStateLOD CreateLODEntity(ISSDescriptor descriptor)
        {
            var lodInfo = SceneLODInfo.Create();
            lodInfo.id = SCENE_ID;
            lodInfo.InitialSceneStateLOD.CurrentState = InitialSceneStateLOD.State.PROCESSING;

            world.Create(lodInfo, sceneDefinition, descriptor);
            return lodInfo.InitialSceneStateLOD;
        }

        private Entity FindHelperEntity()
        {
            Entity found = Entity.Null;
            QueryDescription query = new QueryDescription().WithAll<ISSAssetCreationHelper, AssetBundlePromise>();

            world.Query(in query, entity =>
            {
                if (found == Entity.Null) found = entity;
            });

            return found;
        }

        private void DeliverResult(Entity helperEntity, StreamableLoadingResult<AssetBundleData> result)
        {
            AssetBundlePromise promise = world.Get<AssetBundlePromise>(helperEntity);
            world.Add(promise.Entity, result);
        }

        private static ISSDescriptorAsset NewDescriptorEntry(string hash) =>
            new ()
            {
                hash = hash,
                position = Vector3.zero,
                rotation = Quaternion.identity,
                scale = Vector3.one,
            };

        private static GltfContainerAsset MakeFakeGltf(string label) =>
            GltfContainerAsset.Create(new GameObject($"fake_{label}"), IStreamableRefCountData.Null.INSTANCE);

        private static GltfContainerAsset MakeFakeGltfWithRenderer(string label, out Renderer renderer)
        {
            var go = new GameObject($"fake_{label}");
            renderer = go.AddComponent<MeshRenderer>();
            GltfContainerAsset asset = GltfContainerAsset.Create(go, IStreamableRefCountData.Null.INSTANCE);
            asset.Renderers.Add(renderer);
            return asset;
        }

        /// <summary>
        ///     Pool-style stub mirroring <see cref="GltfContainerAssetsCache" />: <c>TryGet</c> removes,
        ///     <c>Dereference</c> returns. Per-key counters are what the tests assert against.
        /// </summary>
        private class TrackingGltfCache : IGltfContainerAssetsCache
        {
            private readonly Dictionary<string, Stack<GltfContainerAsset>> stash = new ();
            private readonly Dictionary<string, int> outstanding = new ();
            private readonly Dictionary<string, int> dereferenceCalls = new ();

            public void Stash(string key, GltfContainerAsset asset)
            {
                if (!stash.TryGetValue(key, out Stack<GltfContainerAsset> entries))
                    stash[key] = entries = new Stack<GltfContainerAsset>();

                entries.Push(asset);
            }

            public int Outstanding(string key) =>
                outstanding.TryGetValue(key, out int n) ? n : 0;

            public int DereferenceCalls(string key) =>
                dereferenceCalls.TryGetValue(key, out int n) ? n : 0;

            public bool TryGet(in string key, out GltfContainerAsset? asset)
            {
                if (stash.TryGetValue(key, out Stack<GltfContainerAsset> entries) && entries.Count > 0)
                {
                    asset = entries.Pop();
                    outstanding[key] = Outstanding(key) + 1;
                    return true;
                }

                asset = null;
                return false;
            }

            public void Dereference(in string key, GltfContainerAsset asset, bool putInBridge = false, bool handleAssetLoad = true)
            {
                outstanding[key] = Math.Max(0, Outstanding(key) - 1);
                dereferenceCalls[key] = DereferenceCalls(key) + 1;

                if (!stash.TryGetValue(key, out Stack<GltfContainerAsset> entries))
                    stash[key] = entries = new Stack<GltfContainerAsset>();

                entries.Push(asset);

                // Mirror GltfContainerAssetsCache.DereferenceFinalOperation: the real cache reparents the
                // asset's Root out of the LOD container when it is returned to the pool. Without this the
                // pooled asset stays a child of InitialSceneStateLOD.ParentContainer, so Dispose's
                // SafeDestroy(ParentContainer) cascades into it and destroys its renderers — the asset (and
                // its renderers) must survive intact for reuse.
                if (asset.Root != null)
                    asset.Root.transform.SetParent(null, true);
            }

            public void Unload(IPerformanceBudget frameTimeBudget, int maxUnloadAmount) { }

            public void SetAssetLoadCache(AssetPreLoadCache assetPreLoadCache) { }
        }
    }
}
