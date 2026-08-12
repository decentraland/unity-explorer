using Arch.Core;
using DCL.Ipfs;
using DCL.LOD.Components;
using DCL.LOD.Systems;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using ECS.SceneLifeCycle.Reporting;
using DCL.SceneRunner.Scene;
using ECS.SceneLifeCycle.SceneDefinition;
using ECS.StreamableLoading.AssetBundles.InitialSceneState;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Cache;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Assert = UnityEngine.Assertions.Assert;

namespace DCL.LOD.Tests
{
    public class UpdateSceneLODInfoSystemShould : UnitySystemTestBase<UpdateSceneLODInfoSystem>
    {
        private const string fakeHash = "FAKE_HASH";
        private SceneLODInfo sceneLODInfo;
                private PartitionComponent partitionComponent = null!;
        private SceneDefinitionComponent sceneDefinitionComponent;

        [SetUp]
        public void Setup()
        {
            ILODSettingsAsset? lodSettings = Substitute.For<ILODSettingsAsset>();

            int[] bucketThresholds =
            {
                2,
            };

            lodSettings.LodPartitionBucketThresholds.Returns(bucketThresholds);

            IScenesCache? scenesCache = Substitute.For<IScenesCache>();
            ISceneReadinessReportQueue? sceneReadinessReportQueue = Substitute.For<ISceneReadinessReportQueue>();

            partitionComponent = new PartitionComponent();

            var sceneEntityDefinition = new SceneEntityDefinition
            {
                id = fakeHash, metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = new Vector2Int[]
                        {
                            new (0, 0), new (0, 1), new (1, 0), new (2, 0), new (2, 1), new (3, 0), new (3, 1),
                        },
                    },
                },
            };

            sceneDefinitionComponent = SceneDefinitionComponentFactory.CreateFromDefinition(sceneEntityDefinition, new IpfsPath());

            sceneLODInfo = SceneLODInfo.Create();
            sceneLODInfo.metadata = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);
            system = new UpdateSceneLODInfoSystem(world, lodSettings);
        }

        [Test]
        //Note: Test modified due to LOD level always defaulting to 3 while we rebuild all of them
        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 1)]
        [TestCase(10, 1)]
        public void ResolveLODLevelWithUnsupportedISS(byte bucket, int expectedLODLevel)
        {
            //Arrange
            partitionComponent.IsDirty = true;
            partitionComponent.Bucket = bucket;
            Entity entity = world.Create(sceneLODInfo, partitionComponent, sceneDefinitionComponent, SceneLoadingState.CreateBuiltScene(), ISSDescriptor.NONE);

            //Act
            system!.Update(0);

            var sceneLODInfoRetrieved = world.Get<SceneLODInfo>(entity);
            Assert.AreEqual(sceneLODInfoRetrieved.CurrentLODLevelPromise, expectedLODLevel);
        }

        // These build their own instrumented ILODSettingsAsset + system, independent of [SetUp]'s `system`.

        private UpdateSceneLODInfoSystem BuildCountingSystem(int[] thresholds, int[] thresholdReadCounter)
        {
            var settings = Substitute.For<ILODSettingsAsset>();
            // Every read of LodPartitionBucketThresholds bumps the counter. On the FullQuality==true path the consumers
            // are the frame-level thresholds-change probe (exactly one read per Update, scene-count independent) and
            // GetLODLevelForPartition — so a gated (skipped) frame produces exactly one read total.
            settings.LodPartitionBucketThresholds.Returns(_ =>
            {
                thresholdReadCounter[0]++;
                return thresholds;
            });
            return new UpdateSceneLODInfoSystem(world, settings);
        }

        private SceneDefinitionComponent MakeDefinition(string id)
        {
            var definition = new SceneEntityDefinition
            {
                id = id, metadata = new SceneMetadata
                {
                    scene = new SceneMetadataScene
                    {
                        DecodedBase = new Vector2Int(0, 0), DecodedParcels = new Vector2Int[] { new (0, 0) },
                    },
                },
            };
            return SceneDefinitionComponentFactory.CreateFromDefinition(definition, new IpfsPath());
        }

        private (Entity entity, SceneLoadingState state, PartitionComponent partition, LODCacheInfo meta) CreateResidentScene(string id, byte bucket, bool fullQuality)
        {
            var info = SceneLODInfo.Create();
            var meta = new LODCacheInfo(new GameObject().AddComponent<LODGroup>(), 2);
            info.metadata = meta;

            var partition = new PartitionComponent { Bucket = bucket, IsBehind = false, IsDirty = false };
            var state = SceneLoadingState.CreateBuiltScene(); // FullQuality == true
            if (!fullQuality) state.FullQuality = false;

            Entity entity = world.Create(info, partition, MakeDefinition(id), state, ISSDescriptor.NONE);
            return (entity, state, partition, meta);
        }

        // Clean, unchanged resident scenes must NOT re-run the LOD threshold scan each frame.
        [Test]
        public void SkipsCleanUnchangedScenes_Performance()
        {
            var counter = new int[1];
            system = BuildCountingSystem(new[] { 2 }, counter);

            const int N = 8;
            for (int i = 0; i < N; i++)
                CreateResidentScene($"scene_{i.ToString()}", 2, true);

            system.Update(0);              // primes: evaluates every scene + records the eval signature
            Assert.IsTrue(counter[0] > 0); // sanity — the first frame really did evaluate

            counter[0] = 0;
            system.Update(0);              // all scenes clean (IsDirty false) + inputs unchanged

            // Only the frame-level thresholds probe reads (1, independent of N); the gate short-circuits before
            // GetLODLevelForPartition.
            Assert.AreEqual(1, counter[0]);
        }

        // FullQuality is NOT covered by PartitionComponent.IsDirty, so a change to it must re-open the gate
        // (the correctness gate, not just an optimization).
        [Test]
        public void ReEvaluatesWhenFullQualityChanges()
        {
            var counter = new int[1];
            system = BuildCountingSystem(new[] { 2 }, counter);

            var s = CreateResidentScene("scene_fq", 2, true);
            system.Update(0);
            counter[0] = 0;

            s.state.FullQuality = false;   // input outside the partition dirty-tracking
            system.Update(0);

            Assert.IsTrue(counter[0] > 1); // beyond the frame probe's single read: gate re-opened -> scene re-evaluated
        }

        // LodPartitionBucketThresholds values are runtime-mutable (quality presets and the LOD debug tools write
        // elements in place) and sit outside both partition dirty-tracking and the per-scene eval signature, so a value
        // change must re-open the gate for clean resident scenes.
        [Test]
        public void ReEvaluatesWhenThresholdsChange()
        {
            var counter = new int[1];
            int[] thresholds = { 2 };
            system = BuildCountingSystem(thresholds, counter);

            var s = CreateResidentScene("scene_thresholds", 2, true);
            system.Update(0);
            Assert.AreEqual(1, (int)world.Get<SceneLODInfo>(s.entity).CurrentLODLevelPromise); // bucket 2 >= threshold 2 -> LOD 1
            counter[0] = 0;

            thresholds[0] = 5;             // element mutated in place, partition untouched (clean)
            system.Update(0);

            Assert.IsTrue(counter[0] > 1); // beyond the frame probe's single read: the scene re-scanned
            // ...and the new threshold actually applied: bucket 2 < 5 -> LOD 0 acquisition started.
            Assert.AreEqual(0, (int)world.Get<SceneLODInfo>(s.entity).CurrentLODLevelPromise);
        }

        // A dirty partition (bucket/behind moved) must re-open the gate.
        [Test]
        public void ReEvaluatesWhenPartitionDirty()
        {
            var counter = new int[1];
            system = BuildCountingSystem(new[] { 2 }, counter);

            var s = CreateResidentScene("scene_dirty", 2, true);
            system.Update(0);
            counter[0] = 0;

            s.partition.IsDirty = true;
            system.Update(0);

            Assert.IsTrue(counter[0] > 1); // beyond the frame probe's single read
        }

        // LODChangeRelativeDistance changes when a LOD resolves and drives the force-LOD0 path in
        // GetLODLevelForPartition on a CLEAN partition. The gate tracks it, so it must re-open here — otherwise a
        // needed LOD acquisition would be stranded.
        [Test]
        public void ReEvaluatesWhenLODLoadStateChanges()
        {
            var counter = new int[1];
            system = BuildCountingSystem(new[] { 2 }, counter);

            var s = CreateResidentScene("scene_lod", 2, true);
            system.Update(0);
            counter[0] = 0;

            s.meta.LODChangeRelativeDistance = 999f; // mirrors what AddSuccessLOD does after a LOD instantiates
            system.Update(0);

            Assert.IsTrue(counter[0] > 1); // beyond the frame probe's single read
        }

        // The AssetBundleManifestVersion for a LOD level is memoized once and reused across scenes/frames instead of
        // being rebuilt per call.
        [Test]
        public void MemoizesLODManifestPerLevel_Performance()
        {
            var counter = new int[1];
            system = BuildCountingSystem(new[] { 2 }, counter);

            var a = CreateResidentScene("scene_a", 2, true); // bucket 2 -> LOD level 1 -> StartLODPromise builds a promise
            var b = CreateResidentScene("scene_b", 2, true);
            system.Update(0);

            var infoA = world.Get<SceneLODInfo>(a.entity);
            var infoB = world.Get<SceneLODInfo>(b.entity);

            AssetBundleManifestVersion manifestA = infoA.CurrentLODPromise.LoadingIntention.AssetBundleManifest;
            AssetBundleManifestVersion manifestB = infoB.CurrentLODPromise.LoadingIntention.AssetBundleManifest;

            // Guard against a FAILED-sentinel false positive: prove these are real LOD manifests.
            Assert.AreEqual("LOD/1", manifestA.GetAssetBundleManifestVersion());
            Assert.AreEqual("LOD/1", manifestB.GetAssetBundleManifestVersion());

            // Both scenes at the same level share ONE memoized instance.
            Assert.IsTrue(ReferenceEquals(manifestA, manifestB));

            // Correctness: the shared manifest still yields per-scene CDN request hashes.
            Assert.AreNotEqual(infoA.CurrentLODPromise.LoadingIntention.Hash,
                infoB.CurrentLODPromise.LoadingIntention.Hash);
        }

        // SceneLODInfo's GetLODs()/SetLODs() roundtrips reuse one owned LOD[] per scene instead of allocating a throwaway
        // array each call.
        [Test]
        public void ReusesLODBufferAcrossRoundtrips_Performance()
        {
            var lodGroup = new GameObject("lodgroup_reuse").AddComponent<LODGroup>();
            lodGroup.SetLODs(new UnityEngine.LOD[]
            {
                new (1f, new Renderer[0]),
                new (0.5f, new Renderer[0]),
            });

            var info = SceneLODInfo.Create();
            info.metadata = new LODCacheInfo(lodGroup, 2);

            // Buffer is lazily seeded — untouched before the first roundtrip.
            Assert.IsNull(info.metadata.ReusableLODsBufferForTests);

            info.RecalculateLODDistances(60f, 1f, 4, 4);

            // The roundtrip seeded and retained the owned buffer.
            UnityEngine.LOD[] first = info.metadata.ReusableLODsBufferForTests!;
            Assert.IsNotNull(first);

            info.RecalculateLODDistances(60f, 1f, 4, 4);

            // Same instance reused across calls, not re-allocated.
            Assert.IsTrue(ReferenceEquals(first, info.metadata.ReusableLODsBufferForTests));

            // And distinct from a fresh native snapshot — proving we hold our own copy, not per-call GetLODs().
            Assert.IsFalse(ReferenceEquals(first, lodGroup.GetLODs()));
        }
    }
}
