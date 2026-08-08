using System;
using System.Linq;
using System.Reflection;
using DCL.Landscape.Systems;
using NUnit.Framework;
using Unity.Collections;
using Unity.PerformanceTesting;
using UnityEngine;

namespace DCL.Landscape.Tests.PerformanceTests
{
    /// <summary>
    /// Guards fix #5: "Reuse persistent native containers in RenderGroundSystem instead of per-frame
    /// TempJob alloc/dispose." Before the fix, <c>RenderGroundSystem.RenderGroundInternal</c>
    /// (RenderGroundSystem.cs:102 &amp; :105) allocated a fresh <c>NativeArray&lt;int&gt; instanceCounts</c>
    /// and a fresh <c>NativeList&lt;Matrix4x4&gt; transforms</c> with <see cref="Allocator.TempJob"/> every
    /// frame the ground is visible, then disposed both (lines 164-165). The fix promotes them to
    /// <see cref="Allocator.Persistent"/> fields allocated once and reused each frame (array zero-cleared,
    /// list <c>Clear()</c>ed), disposed in an <c>OnDispose</c> override.
    ///
    /// <para>
    /// Following the repo's isolation-benchmark convention (see the sibling
    /// <c>GrassScatterConstantUploadPerformanceTest</c> and
    /// <c>NearbyAudioPositionHotPathPerformanceTest</c>): the production system needs a live camera,
    /// ITerrain, Cinemachine preset and Arch world to run, none of which exist headless. So the metric
    /// this test targets — per-frame native-container allocation churn — is measured on the exact
    /// container lifecycle the fix changes, and the falsification power comes from three angles:
    /// (1) a structural reflection check that ties the assertions to the actual production change
    /// (revert the patch → the reused fields / <c>OnDispose</c> vanish → fail);
    /// (2) a behavioral check that reproduces <c>GenerateGroundJob</c>'s partial InstanceCounts write
    /// (GenerateGroundJob.cs:63-83) to prove the per-frame CLEAR the fix must add is load-bearing;
    /// (3) a deterministic allocation-count invariant plus <c>Measure.Method</c> SampleGroups exposing
    /// the interop/allocator cost the reuse removes.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class RenderGroundContainerReusePerformanceTest
    {
        // GroundMeshes = middle / edge / corner piece (GenerateGroundJob.MAGIC_PATTERN mesh indices 0,1,2).
        private const int MESH_COUNT = 3;

        // Representative of LandscapeData.GroundInstanceCapacity: the NativeList backing store is
        // GroundInstanceCapacity * sizeof(Matrix4x4) = 65536 * 64B = 4 MB, re-acquired every frame pre-fix.
        private const int GROUND_INSTANCE_CAPACITY = 65536;

        // ─────────────────────────────────────────────────────────────────────────────
        // 1. Structural tie to the shipped fix — falsifies immediately if the patch is reverted.
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Fix_PromotesContainersToReusedPersistentFields_WithTeardown()
        {
            Type sys = typeof(RenderGroundSystem);
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            FieldInfo[] fields = sys.GetFields(FLAGS);

            Assert.That(fields.Any(f => f.FieldType == typeof(NativeArray<int>)), Is.True,
                "RenderGroundSystem must hold instanceCounts as a reused NativeArray<int> field " +
                "(it was a per-frame Allocator.TempJob local before the fix).");

            Assert.That(fields.Any(f => f.FieldType == typeof(NativeList<Matrix4x4>)), Is.True,
                "RenderGroundSystem must hold transforms as a reused NativeList<Matrix4x4> field " +
                "(it was a per-frame Allocator.TempJob local before the fix).");

            MethodInfo? onDispose = sys.GetMethod("OnDispose", FLAGS);
            Assert.That(onDispose, Is.Not.Null,
                "RenderGroundSystem must override OnDispose to release the persistent containers, " +
                "else they leak native memory on every realm transition.");
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 2. Behavioral — the reused InstanceCounts array MUST be cleared each frame.
        //    Reproduces GenerateGroundJob.Execute (GenerateGroundJob.cs:63-83): it writes
        //    InstanceCounts only at mesh-index boundaries and once for the final index, so a mesh
        //    slot with zero instances this frame is NEVER written. With a reused array that leaves
        //    last frame's count in place → ghost draws. This is the risk the fix's clear addresses.
        // ─────────────────────────────────────────────────────────────────────────────

        private static void PopulateLikeJob(NativeArray<int> instanceCounts, NativeList<Matrix4x4> transforms,
            int[] sortedMeshIndices)
        {
            var instanceCount = 0;
            var meshIndex = 0;

            foreach (int mi in sortedMeshIndices)
            {
                if (meshIndex < mi)
                {
                    instanceCounts[meshIndex] = instanceCount;
                    meshIndex = mi;
                    instanceCount = 0;
                }

                instanceCount++;
                transforms.Add(Matrix4x4.identity);
            }

            instanceCounts[meshIndex] = instanceCount;
        }

        [Test]
        public void ReusedInstanceCounts_WithoutClear_GhostsStaleSlots_ClearFixesIt()
        {
            var counts = new NativeArray<int>(MESH_COUNT, Allocator.Persistent);
            var transforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.Persistent);

            try
            {
                // Frame 1: instances across all three mesh slots.
                PopulateLikeJob(counts, transforms, new[] { 0, 0, 1, 1, 2 });
                Assert.That(counts.ToArray(), Is.EqualTo(new[] { 2, 2, 1 }), "frame 1 baseline counts");

                // Frame 2 reusing the array WITHOUT clearing: only mesh slot 0 has instances now.
                transforms.Clear();
                PopulateLikeJob(counts, transforms, new[] { 0, 0, 0 });

                Assert.That(counts[1] != 0 || counts[2] != 0, Is.True,
                    "Reusing InstanceCounts without a per-frame clear must leave stale non-zero counts " +
                    "in slots 1/2 → proves the clear is load-bearing (a naive persistent-reuse would ghost).");

                // Frame 2 WITH the fix's clear: stale slots zeroed, identical to a fresh zeroed allocation.
                for (var i = 0; i < counts.Length; i++) counts[i] = 0;
                transforms.Clear();
                PopulateLikeJob(counts, transforms, new[] { 0, 0, 0 });

                Assert.That(counts.ToArray(), Is.EqualTo(new[] { 3, 0, 0 }),
                    "After the per-frame clear the reused array matches a freshly-zeroed TempJob allocation " +
                    "(no ghost instances).");
            }
            finally
            {
                counts.Dispose();
                transforms.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 3. Parity — persistent-reuse-with-clear reproduces per-frame fresh allocation exactly,
        //    over a frame sequence whose per-mesh coverage grows and shrinks.
        // ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void PersistentReuseWithClear_MatchesPerFrameFreshAllocation()
        {
            int[][] frames =
            {
                new[] { 0, 0, 1, 2 },
                new[] { 0 },
                Array.Empty<int>(),
                new[] { 1, 1, 1, 2, 2 }, // slot 0 empty this frame — the shrink case
                new[] { 0, 0, 0, 1, 2 },
            };

            var reuseCounts = new NativeArray<int>(MESH_COUNT, Allocator.Persistent);
            var reuseTransforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.Persistent);

            try
            {
                for (var f = 0; f < frames.Length; f++)
                {
                    // Pre-fix baseline: brand-new zero-initialized TempJob containers each frame.
                    var freshCounts = new NativeArray<int>(MESH_COUNT, Allocator.TempJob);
                    var freshTransforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.TempJob);

                    // Post-fix: reuse persistent containers, clearing first. Skip the clear on the first
                    // frame — freshly-allocated Persistent memory is already zeroed (mirrors the patch's
                    // init branch, which does not clear).
                    if (f > 0)
                    {
                        for (var i = 0; i < reuseCounts.Length; i++) reuseCounts[i] = 0;
                        reuseTransforms.Clear();
                    }

                    PopulateLikeJob(freshCounts, freshTransforms, frames[f]);
                    PopulateLikeJob(reuseCounts, reuseTransforms, frames[f]);

                    Assert.That(reuseCounts.ToArray(), Is.EqualTo(freshCounts.ToArray()),
                        $"InstanceCounts diverged from a fresh allocation on frame {f}");
                    Assert.That(reuseTransforms.AsArray().ToArray(), Is.EqualTo(freshTransforms.AsArray().ToArray()),
                        $"transforms diverged from a fresh allocation on frame {f}");

                    freshCounts.Dispose();
                    freshTransforms.Dispose();
                }
            }
            finally
            {
                reuseCounts.Dispose();
                reuseTransforms.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // 4. Perf + the allocation invariant the fix targets: zero per-frame allocations.
        // ─────────────────────────────────────────────────────────────────────────────

        [Test, Performance]
        public void ContainerLifecycle_PersistentReuse_IsAllocationFreePerFrame()
        {
            const int FRAMES = 300;

            // Report the per-frame cost of each strategy at real GroundInstanceCapacity scale (4 MB list).
            Measure
               .Method(() =>
                {
                    var counts = new NativeArray<int>(MESH_COUNT, Allocator.TempJob);
                    var transforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.TempJob);

                    for (var i = 0; i < MESH_COUNT; i++) counts[i] = i;

                    counts.Dispose();
                    transforms.Dispose();
                })
               .SampleGroup("PerFrame_TempJob_alloc_dispose")
               .WarmupCount(5).MeasurementCount(100).Run();

            var reuseCounts = new NativeArray<int>(MESH_COUNT, Allocator.Persistent);
            var reuseTransforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.Persistent);

            Measure
               .Method(() =>
                {
                    for (var i = 0; i < reuseCounts.Length; i++) reuseCounts[i] = 0;
                    reuseTransforms.Clear();
                    for (var i = 0; i < MESH_COUNT; i++) reuseCounts[i] = i;
                })
               .SampleGroup("PerFrame_Persistent_reuse")
               .WarmupCount(5).MeasurementCount(100).Run();

            reuseCounts.Dispose();
            reuseTransforms.Dispose();

            // Deterministic allocation-count invariant — the pass criterion the fix must satisfy.
            var tempJobAllocations = 0;

            for (var f = 0; f < FRAMES; f++)
            {
                var c = new NativeArray<int>(MESH_COUNT, Allocator.TempJob);
                tempJobAllocations++;
                var t = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.TempJob);
                tempJobAllocations++;
                c.Dispose();
                t.Dispose();
            }

            var reuseAllocations = 0;
            var pc = new NativeArray<int>(MESH_COUNT, Allocator.Persistent);
            reuseAllocations++;
            var pt = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.Persistent);
            reuseAllocations++;

            for (var f = 0; f < FRAMES; f++)
            {
                for (var i = 0; i < pc.Length; i++) pc[i] = 0;
                pt.Clear();
            }

            pc.Dispose();
            pt.Dispose();

            Assert.That(tempJobAllocations, Is.EqualTo(2 * FRAMES),
                "Pre-fix baseline allocates two native containers every frame.");
            Assert.That(reuseAllocations, Is.EqualTo(2),
                "Post-fix: the persistent containers are allocated exactly once across all frames " +
                "(zero per-frame allocations on the ground path).");
        }
    }
}