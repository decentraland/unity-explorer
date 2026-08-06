using System.Collections.Generic;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using ECS.Unity.Systems;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.PerformanceTesting;
using UnityEngine;

namespace ECS.Unity.Prioritization.Tests.PerformanceTests
{
    /// <summary>
    /// Falsification harness for the jobified partition math (fix #6):
    /// <see cref="PartitionAssetEntitiesSystem.ComputePartition"/> is extracted from the historic
    /// per-entity <c>RePartition</c>/<c>ResolvePartitionFromDistance</c> main-thread path and shared
    /// verbatim by a Burst <see cref="IJobParallelFor"/> and the managed fallback.
    /// <para>
    /// (A) <see cref="Parity_ManagedReference_vs_JobPath"/> is the primary falsifier: it runs the
    /// unchanged managed reference (the real public <see cref="PartitionAssetEntitiesSystem.ResolvePartitionFromDistance"/>
    /// plus the fast-path inline) against BOTH the managed <c>ComputePartition</c> and the scheduled
    /// job over identical inputs and asserts <c>Bucket</c>, <c>IsBehind</c> and <c>RawSqrDistance</c>
    /// are bit-identical for every entity. Any divergence fails the fix.
    /// </para>
    /// <para>
    /// (B) <see cref="ManagedLoop_vs_JobSchedule"/> records the wall-clock of the managed loop vs the
    /// Schedule+Complete job over the same 5000-entity buffer as comparable <see cref="SampleGroup"/>s.
    /// The perf delta is Burst-availability dependent in-editor, so it is measured, not hard-asserted
    /// here — parity in (A) is the gate. See notes in <see cref="ManagedLoop_vs_JobSchedule"/>.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class PartitionAssetEntitiesPerformanceTest
    {
        private const int ENTITY_COUNT = 5000;
        private const int SEED = 1234;

        // FastPathSqrDistance chosen so the randomized cloud spans BOTH branches:
        // a ~±200m cube yields sqr distances up to ~1.2e5, well past the 1.6e4 gate, while
        // plenty of samples fall inside the near/bucket-scan path.
        private const int FAST_PATH_SQR = 16384;
        private const byte SCENE_BUCKET = 5;
        private const bool SCENE_IS_BEHIND = true;

        private static readonly int[] BUCKETS = { 64, 256, 1024, 4096, 16384 };

        private NativeArray<float3> positions;
        private NativeArray<int> sqrBuckets;
        private NativeArray<float> rawIn;
        private NativeArray<byte> outBucket;
        private NativeArray<bool> outIsBehind;
        private NativeArray<float> outRaw;

        private float3 cameraPosition;
        private float3 cameraForward;

        [SetUp]
        public void SetUp()
        {
            UnityEngine.Random.InitState(SEED);

            positions = new NativeArray<float3>(ENTITY_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            rawIn = new NativeArray<float>(ENTITY_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outBucket = new NativeArray<byte>(ENTITY_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outIsBehind = new NativeArray<bool>(ENTITY_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            outRaw = new NativeArray<float>(ENTITY_COUNT, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < ENTITY_COUNT; i++)
            {
                positions[i] = (float3)(UnityEngine.Random.insideUnitSphere * 200f);

                // Deterministic non-trivial starting RawSqrDistance so the fast-path pass-through
                // (raw must remain untouched) is actually exercised and verified.
                rawIn[i] = i * 0.5f;
            }

            sqrBuckets = new NativeArray<int>(BUCKETS.Length, Allocator.Persistent);
            for (var i = 0; i < BUCKETS.Length; i++)
                sqrBuckets[i] = BUCKETS[i];

            cameraPosition = new float3(3f, 1.6f, -5f);
            cameraForward = math.normalize(new float3(0.2f, -0.1f, 1f));
        }

        [TearDown]
        public void TearDown()
        {
            if (positions.IsCreated) positions.Dispose();
            if (sqrBuckets.IsCreated) sqrBuckets.Dispose();
            if (rawIn.IsCreated) rawIn.Dispose();
            if (outBucket.IsCreated) outBucket.Dispose();
            if (outIsBehind.IsCreated) outIsBehind.Dispose();
            if (outRaw.IsCreated) outRaw.Dispose();
        }

        /// <summary>
        /// Exact reimplementation of the pre-fix managed <c>RePartition</c> semantics, used as the
        /// oracle. Fast path (sqrDistance &gt; FastPathSqrDistance) inherits scene bucket/behind and
        /// LEAVES RawSqrDistance untouched; otherwise defers to the still-live production
        /// <see cref="PartitionAssetEntitiesSystem.ResolvePartitionFromDistance"/>.
        /// </summary>
        private static void Reference(IPartitionSettings settings, Vector3 cam, Vector3 fwd, Vector3 pos, PartitionComponent pc)
        {
            Vector3 vectorToCamera = pos - cam;
            float sqrDistance = Vector3.SqrMagnitude(vectorToCamera);

            if (sqrDistance > settings.FastPathSqrDistance)
            {
                pc.Bucket = SCENE_BUCKET;
                pc.IsBehind = SCENE_IS_BEHIND;
            }
            else
                PartitionAssetEntitiesSystem.ResolvePartitionFromDistance(settings, fwd, pc, sqrDistance, vectorToCamera);
        }

        [Test]
        public void Parity_ManagedReference_vs_JobPath()
        {
            var settings = new FakePartitionSettings(BUCKETS, FAST_PATH_SQR);

            // Run the whole jobified path exactly as production does (Schedule + Complete).
            new PartitionMathJob
            {
                CameraPosition = cameraPosition,
                CameraForward = cameraForward,
                FastPathSqrDistance = FAST_PATH_SQR,
                SceneBucket = SCENE_BUCKET,
                SceneIsBehind = SCENE_IS_BEHIND,
                SqrDistanceBuckets = sqrBuckets,
                EntityPositions = positions,
                RawIn = rawIn,
                OutBucket = outBucket,
                OutIsBehind = outIsBehind,
                OutRaw = outRaw,
            }.Schedule(ENTITY_COUNT, 64).Complete();

            var mismatches = 0;

            for (var i = 0; i < ENTITY_COUNT; i++)
            {
                Vector3 pos = (Vector3)positions[i];

                var reference = new PartitionComponent { RawSqrDistance = rawIn[i] };
                Reference(settings, (Vector3)cameraPosition, (Vector3)cameraForward, pos, reference);

                // Managed shared-math path (must equal the reference by construction).
                PartitionAssetEntitiesSystem.ComputePartition(
                    positions[i], cameraPosition, cameraForward,
                    FAST_PATH_SQR, sqrBuckets, SCENE_BUCKET, SCENE_IS_BEHIND, rawIn[i],
                    out byte managedBucket, out bool managedBehind, out float managedRaw);

                bool managedOk = managedBucket == reference.Bucket
                                 && managedBehind == reference.IsBehind
                                 && managedRaw.Equals(reference.RawSqrDistance);

                // Jobbed path result (Burst-compiled when available).
                bool jobOk = outBucket[i] == reference.Bucket
                             && outIsBehind[i] == reference.IsBehind
                             && outRaw[i].Equals(reference.RawSqrDistance);

                if (!managedOk || !jobOk)
                {
                    mismatches++;

                    if (mismatches <= 10)
                        Debug.Log(
                            $"MISMATCH [{i}] pos={pos} ref(b={reference.Bucket},beh={reference.IsBehind},raw={reference.RawSqrDistance}) " +
                            $"managed(b={managedBucket},beh={managedBehind},raw={managedRaw}) " +
                            $"job(b={outBucket[i]},beh={outIsBehind[i]},raw={outRaw[i]})");
                }
            }

            Assert.AreEqual(0, mismatches,
                $"Partition parity broken for {mismatches}/{ENTITY_COUNT} entities — jobified math diverges from the managed reference. Fix #6 falsified.");
        }

        [Test]
        [Performance]
        public void ManagedLoop_vs_JobSchedule()
        {
            var managed = new SampleGroup("Partition.Managed.5000", SampleUnit.Microsecond);
            var jobbed = new SampleGroup("Partition.Job.5000", SampleUnit.Microsecond);

            Measure.Method(() =>
                    {
                        for (var i = 0; i < ENTITY_COUNT; i++)
                        {
                            PartitionAssetEntitiesSystem.ComputePartition(
                                positions[i], cameraPosition, cameraForward,
                                FAST_PATH_SQR, sqrBuckets, SCENE_BUCKET, SCENE_IS_BEHIND, rawIn[i],
                                out byte bucket, out bool isBehind, out float raw);

                            // NativeArray's indexer is a property, so results are written via locals.
                            outBucket[i] = bucket;
                            outIsBehind[i] = isBehind;
                            outRaw[i] = raw;
                        }
                    })
                   .SampleGroup(managed)
                   .WarmupCount(5)
                   .MeasurementCount(50)
                   .Run();

            Measure.Method(() =>
                    {
                        new PartitionMathJob
                        {
                            CameraPosition = cameraPosition,
                            CameraForward = cameraForward,
                            FastPathSqrDistance = FAST_PATH_SQR,
                            SceneBucket = SCENE_BUCKET,
                            SceneIsBehind = SCENE_IS_BEHIND,
                            SqrDistanceBuckets = sqrBuckets,
                            EntityPositions = positions,
                            RawIn = rawIn,
                            OutBucket = outBucket,
                            OutIsBehind = outIsBehind,
                            OutRaw = outRaw,
                        }.Schedule(ENTITY_COUNT, 64).Complete();
                    })
                   .SampleGroup(jobbed)
                   .WarmupCount(5)
                   .MeasurementCount(50)
                   .Run();

            // Both SampleGroups are reported for comparison. The >=30% speedup in the spec is a
            // play-mode, Burst-enabled, worker-saturated target; in-editor Burst may be disabled and
            // the job-system scheduling floor can dominate at this element count, so a speedup
            // assertion here would be flaky. Correctness parity (test A) is the hard falsifier.
            // Timings are reported via SampleGroups Partition.Managed.5000 / Partition.Job.5000.
        }

        [BurstCompile(FloatMode = FloatMode.Strict)]
        private struct PartitionMathJob : IJobParallelFor
        {
            public float3 CameraPosition;
            public float3 CameraForward;
            public float FastPathSqrDistance;
            public byte SceneBucket;
            public bool SceneIsBehind;

            [ReadOnly] public NativeArray<int> SqrDistanceBuckets;
            [ReadOnly] public NativeArray<float3> EntityPositions;
            [ReadOnly] public NativeArray<float> RawIn;

            [WriteOnly] public NativeArray<byte> OutBucket;
            [WriteOnly] public NativeArray<bool> OutIsBehind;
            [WriteOnly] public NativeArray<float> OutRaw;

            public void Execute(int index)
            {
                PartitionAssetEntitiesSystem.ComputePartition(
                    EntityPositions[index], CameraPosition, CameraForward,
                    FastPathSqrDistance, SqrDistanceBuckets, SceneBucket, SceneIsBehind, RawIn[index],
                    out byte bucket, out bool isBehind, out float raw);

                OutBucket[index] = bucket;
                OutIsBehind[index] = isBehind;
                OutRaw[index] = raw;
            }
        }

        private sealed class FakePartitionSettings : IPartitionSettings
        {
            private readonly int[] buckets;

            public FakePartitionSettings(int[] buckets, int fastPathSqrDistance)
            {
                this.buckets = buckets;
                FastPathSqrDistance = fastPathSqrDistance;
            }

            public float AngleTolerance => 1f;
            public float PositionSqrTolerance => 0.01f;
            public IReadOnlyList<int> SqrDistanceBuckets => buckets;
            public int FastPathSqrDistance { get; }
        }
    }
}