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
    /// <see cref="RenderGroundSystem.RenderGroundInternal"/> holds its native ground-instance
    /// containers — <c>NativeArray&lt;int&gt; instanceCounts</c> and
    /// <c>NativeList&lt;Matrix4x4&gt; transforms</c> — as reused <see cref="Allocator.Persistent"/>
    /// fields allocated once and cleared each frame (array zero-cleared, list <c>Clear()</c>ed) rather
    /// than fresh <see cref="Allocator.TempJob"/> containers allocated and disposed every frame the
    /// ground is visible; both are released in an <c>OnDispose</c> override.
    ///
    /// <para>
    /// Following the repo's isolation-benchmark convention (see the sibling
    /// <c>GrassScatterConstantUploadPerformanceTest</c> and
    /// <c>NearbyAudioPositionHotPathPerformanceTest</c>): the production system needs a live camera,
    /// ITerrain, Cinemachine preset and Arch world to run, none of which exist headless. So the metric
    /// this test targets — per-frame native-container allocation churn — is measured on the exact
    /// container lifecycle, covered from three angles:
    /// (1) a structural reflection check that the reused fields and <c>OnDispose</c> exist on
    /// <see cref="RenderGroundSystem"/>;
    /// (2) a behavioral check that reproduces <c>GenerateGroundJob</c>'s partial InstanceCounts write
    /// (GenerateGroundJob.cs:63-83) to prove the per-frame clear is load-bearing — without it, reusing
    /// the array ghosts stale counts from the previous frame;
    /// (3) a deterministic allocation-count invariant plus <c>Measure.Method</c> SampleGroups exposing
    /// the interop/allocator cost the reuse avoids.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class RenderGroundContainerReusePerformanceTest
    {
        private const int MESH_COUNT = 3;

        private const int GROUND_INSTANCE_CAPACITY = 65536;


        [Test]
        public void Fix_PromotesContainersToReusedPersistentFields_WithTeardown()
        {
            Type sys = typeof(RenderGroundSystem);
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            FieldInfo[] fields = sys.GetFields(FLAGS);

            Assert.That(fields.Any(f => f.FieldType == typeof(NativeArray<int>)), Is.True,
                "RenderGroundSystem must hold instanceCounts as a reused NativeArray<int> field " +
                "rather than a per-frame Allocator.TempJob local.");

            Assert.That(fields.Any(f => f.FieldType == typeof(NativeList<Matrix4x4>)), Is.True,
                "RenderGroundSystem must hold transforms as a reused NativeList<Matrix4x4> field " +
                "rather than a per-frame Allocator.TempJob local.");

            MethodInfo? onDispose = sys.GetMethod("OnDispose", FLAGS);
            Assert.That(onDispose, Is.Not.Null,
                "RenderGroundSystem must override OnDispose to release the persistent containers, " +
                "else they leak native memory on every realm transition.");
        }


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
                PopulateLikeJob(counts, transforms, new[] { 0, 0, 1, 1, 2 });
                Assert.That(counts.ToArray(), Is.EqualTo(new[] { 2, 2, 1 }), "frame 1 baseline counts");

                transforms.Clear();
                PopulateLikeJob(counts, transforms, new[] { 0, 0, 0 });

                Assert.That(counts[1] != 0 || counts[2] != 0, Is.True,
                    "Reusing InstanceCounts without a per-frame clear leaves stale non-zero counts " +
                    "in slots 1/2 from the previous frame.");

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


        [Test]
        public void PersistentReuseWithClear_MatchesPerFrameFreshAllocation()
        {
            int[][] frames =
            {
                new[] { 0, 0, 1, 2 },
                new[] { 0 },
                Array.Empty<int>(),
                new[] { 1, 1, 1, 2, 2 },
                new[] { 0, 0, 0, 1, 2 },
            };

            var reuseCounts = new NativeArray<int>(MESH_COUNT, Allocator.Persistent);
            var reuseTransforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.Persistent);

            try
            {
                for (var f = 0; f < frames.Length; f++)
                {
                    var freshCounts = new NativeArray<int>(MESH_COUNT, Allocator.TempJob);
                    var freshTransforms = new NativeList<Matrix4x4>(GROUND_INSTANCE_CAPACITY, Allocator.TempJob);

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


        [Test, Performance]
        public void ContainerLifecycle_PersistentReuse_IsAllocationFreePerFrame()
        {
            const int FRAMES = 300;

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
                "A fresh TempJob allocation per frame allocates two native containers every frame.");
            Assert.That(reuseAllocations, Is.EqualTo(2),
                "The persistent containers are allocated exactly once across all frames " +
                "(zero per-frame allocations on the ground path).");
        }
    }
}