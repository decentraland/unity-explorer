using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.Rendering.GPUInstanceBatcher.Tests.PerformanceTests
{
    // Correctness + timing harness for the LODBuffersEvaluation compute kernel
    // (ComputeLODBufferAccumulation), which compacts per-instance LOD levels into per-bucket
    // lists inside InstanceLookUpAndDitherBuffer and totals them in arrLODCount.
    //
    // The perf fix replaces one contended global InterlockedAdd per instance on the tiny
    // 8-element arrLODCount with a group-shared reduction (one global atomic per bucket per
    // thread-group). Because the compaction is a SET (order within a bucket is already
    // non-deterministic under the old atomic-race code), correctness is validated against an
    // independent CPU reference that reproduces the exact bucket mapping:
    //   LOD_A (< nLODCount)                               -> bucket nLOD_A,    dither = LOD_Dither
    //   crossfade (nLOD_A in 0..6 AND nLOD_B < nLODCount) -> bucket nLOD_A+1, dither = 0
    //
    // FALSIFICATION: if the group-shared reduction miscounts, drops instances, or collides two
    // instances onto the same compaction slot, either arrLODCount differs from the reference
    // count or the per-bucket written set differs from the reference set - both hard-assert.
    // Dispatch time per scale is recorded as a SampleGroup so the 100k contended case (the one
    // the fix targets) can be compared before/after.
    [Category("Performance")]
    public class LODBufferAccumulationPerformanceTest
    {
        private const string KERNEL = "ComputeLODBufferAccumulation";
        private const string ASSET_PATH = "Assets/DCL/Rendering/GPUInstanceBatcher/ComputeShaders/LODBuffersEvaluation.compute";

        // nLODCount = 8 keeps every crossfade target (nLOD_A + 1) inside the 8 buckets, so the
        // whole valid LOD range is exercised without the arrLODCount[8] out-of-range edge a
        // smaller count would hit.
        private const uint N_LOD_COUNT = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct PerInstanceLODLevelsGPU
        {
            public uint LOD_A;
            public uint LOD_B;
            public uint LOD_Dither;
            public uint LOD_Shadow;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct InstanceLookUpAndDitherGPU
        {
            public uint nInstanceLookUp;
            public uint nDither;
            public uint nPadding0;
            public uint nPadding1;
        }

        // Mirrors the HLSL GroupData struct byte-for-byte (196 bytes). Only nInstBufferSize and
        // nLODCount are read by this kernel; the rest stay zero but MUST be present so the two
        // fields land at the correct offsets.
        [StructLayout(LayoutKind.Sequential)]
        private struct GroupDataGPU
        {
            public Matrix4x4 matLODSizes;
            public Matrix4x4 matCamera_MVP;
            public Vector3 vCameraPosition;
            public float fShadowDistance;
            public Vector3 vBoundsCenter;
            public float fFrustumOffset;
            public Vector3 vBoundsExtents;
            public float fCameraHalfAngle;
            public float fMaxDistance;
            public float fMinCullingDistance;
            public uint nInstBufferSize;
            public uint nLODCount;
        }

        private static ComputeShader LoadShader()
        {
#if UNITY_EDITOR
            var cs = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(ASSET_PATH);
            if (cs != null) return cs;
#endif
            foreach (var candidate in Resources.FindObjectsOfTypeAll<ComputeShader>())
                if (candidate != null && candidate.name == "LODBuffersEvaluation")
                    return candidate;
            return null;
        }

        private static PerInstanceLODLevelsGPU[] BuildInput(int count, bool uniform, int seed)
        {
            var rng = new System.Random(seed);
            var arr = new PerInstanceLODLevelsGPU[count];
            for (int i = 0; i < count; i++)
            {
                uint a, b;
                if (uniform)
                {
                    // Worst-case contention: every instance targets the SAME bucket (4), no crossfade.
                    a = 4;
                    b = N_LOD_COUNT; // invalid => no crossfade
                }
                else
                {
                    // Mixed: a in [0, nLODCount] (== nLODCount means culled, no write); b in [0, nLODCount].
                    a = (uint)rng.Next(0, (int)N_LOD_COUNT + 1);
                    b = (uint)rng.Next(0, (int)N_LOD_COUNT + 1);
                }

                arr[i] = new PerInstanceLODLevelsGPU
                {
                    LOD_A = a,
                    LOD_B = b,
                    LOD_Dither = (uint)rng.Next(0, 256),
                    LOD_Shadow = 0,
                };
            }
            return arr;
        }

        // Independent CPU reference of the exact kernel semantics. Per bucket: instanceId -> dither.
        private static Dictionary<uint, uint>[] ComputeExpected(PerInstanceLODLevelsGPU[] input, out uint[] expectedCounts)
        {
            var buckets = new Dictionary<uint, uint>[8];
            for (int b = 0; b < 8; b++) buckets[b] = new Dictionary<uint, uint>();
            expectedCounts = new uint[8];

            for (uint i = 0; i < input.Length; i++)
            {
                uint a = input[i].LOD_A;
                uint bLod = input[i].LOD_B;
                if (a < N_LOD_COUNT)
                {
                    buckets[a][i] = input[i].LOD_Dither;
                    expectedCounts[a]++;
                    if (a < 7 && bLod < N_LOD_COUNT)
                    {
                        buckets[a + 1][i] = 0u;
                        expectedCounts[a + 1]++;
                    }
                }
            }
            return buckets;
        }

        [Test]
        [Performance]
        [TestCase(1000, true, TestName = "Parity_1k_uniform")]
        [TestCase(1000, false, TestName = "Parity_1k_mixed")]
        [TestCase(10000, true, TestName = "Parity_10k_uniform")]
        [TestCase(10000, false, TestName = "Parity_10k_mixed")]
        [TestCase(100000, true, TestName = "Parity_100k_uniform")]
        [TestCase(100000, false, TestName = "Parity_100k_mixed")]
        public void GroupSharedReduction_MatchesReference_AndRecordsDispatchTime(int count, bool uniform)
        {
            if (!SystemInfo.supportsComputeShaders)
                Assert.Ignore("Compute shaders not supported on this graphics device.");

            ComputeShader cs = LoadShader();
            if (cs == null)
                Assert.Ignore($"Could not load compute shader at {ASSET_PATH}.");

            int kernel = cs.FindKernel(KERNEL);
            Assert.That(kernel, Is.GreaterThanOrEqualTo(0), "Kernel not found.");

            PerInstanceLODLevelsGPU[] input = BuildInput(count, uniform, seed: 1234 + count + (uniform ? 1 : 0));
            Dictionary<uint, uint>[] expected = ComputeExpected(input, out uint[] expectedCounts);

            var group = new GroupDataGPU { nInstBufferSize = (uint)count, nLODCount = N_LOD_COUNT };

            ComputeBuffer lodLevelsBuf = null, groupBuf = null, arrLodBuf = null, lookupBuf = null;
            try
            {
                lodLevelsBuf = new ComputeBuffer(count, Marshal.SizeOf<PerInstanceLODLevelsGPU>());
                lodLevelsBuf.SetData(input);

                groupBuf = new ComputeBuffer(1, Marshal.SizeOf<GroupDataGPU>());
                groupBuf.SetData(new[] { group });

                arrLodBuf = new ComputeBuffer(8, sizeof(uint));

                // 8 bucket strides of `count` entries each (matches nInstBufferSize * bucket + slot).
                lookupBuf = new ComputeBuffer(count * 8, Marshal.SizeOf<InstanceLookUpAndDitherGPU>());

                cs.SetBuffer(kernel, "PerInstance_LODLevels", lodLevelsBuf);
                cs.SetBuffer(kernel, "GroupDataBuffer", groupBuf);
                cs.SetBuffer(kernel, "arrLODCount", arrLodBuf);
                cs.SetBuffer(kernel, "InstanceLookUpAndDitherBuffer", lookupBuf);

                cs.GetKernelThreadGroupSizes(kernel, out uint tgx, out _, out _);
                int groups = Mathf.CeilToInt((float)count / (int)tgx);

                var zeros8 = new uint[8];
                var arrOut = new uint[8];

                // Timed dispatch (kernel + readback forces GPU completion). Records the metric the
                // fix targets - dispatch cost at each scale, especially 100k uniform (max contention).
                var sample = new SampleGroup($"LODAccumulation.Dispatch.{count}.{(uniform ? "uniform" : "mixed")}", SampleUnit.Millisecond);
                Measure.Method(() =>
                    {
                        arrLodBuf.SetData(zeros8);
                        cs.Dispatch(kernel, groups, 1, 1);
                        arrLodBuf.GetData(arrOut); // sync point
                    })
                    .SampleGroup(sample)
                    .WarmupCount(3)
                    .MeasurementCount(15)
                    .Run();

                // Final authoritative dispatch for correctness readback.
                arrLodBuf.SetData(zeros8);
                cs.Dispatch(kernel, groups, 1, 1);
                arrLodBuf.GetData(arrOut);

                var lookup = new InstanceLookUpAndDitherGPU[count * 8];
                lookupBuf.GetData(lookup);

                // 1) arrLODCount byte-identical to the reference per-bucket totals.
                for (int b = 0; b < 8; b++)
                    Assert.That(arrOut[b], Is.EqualTo(expectedCounts[b]),
                        $"arrLODCount[{b}] mismatch (count={count}, uniform={uniform}).");

                // 2) Per bucket: the written set of (instanceId -> dither) matches the reference,
                //    and no two instances collide on the same slot (unique ids within the bucket).
                for (int b = 0; b < 8; b++)
                {
                    int total = (int)arrOut[b];
                    var seen = new Dictionary<uint, uint>(total);
                    for (int k = 0; k < total; k++)
                    {
                        InstanceLookUpAndDitherGPU e = lookup[(b * count) + k];
                        Assert.That(seen.ContainsKey(e.nInstanceLookUp), Is.False,
                            $"Slot collision in bucket {b}: instance {e.nInstanceLookUp} written twice (count={count}, uniform={uniform}).");
                        seen[e.nInstanceLookUp] = e.nDither;
                    }

                    Assert.That(seen.Count, Is.EqualTo(expected[b].Count),
                        $"Bucket {b} written-set size mismatch (count={count}, uniform={uniform}).");

                    foreach (KeyValuePair<uint, uint> kv in expected[b])
                    {
                        Assert.That(seen.TryGetValue(kv.Key, out uint dither), Is.True,
                            $"Bucket {b} missing instance {kv.Key} (count={count}, uniform={uniform}).");
                        Assert.That(dither, Is.EqualTo(kv.Value),
                            $"Bucket {b} instance {kv.Key} dither mismatch (count={count}, uniform={uniform}).");
                    }
                }
            }
            finally
            {
                lodLevelsBuf?.Release();
                groupBuf?.Release();
                arrLodBuf?.Release();
                lookupBuf?.Release();
            }
        }
    }
}
