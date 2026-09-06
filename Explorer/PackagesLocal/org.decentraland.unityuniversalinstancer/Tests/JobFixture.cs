using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// Drives <see cref="CullAndLODJob"/> directly on the calling thread with
    /// hand-built planes so the geometry is exact. The default job sees an
    /// axis-aligned box "frustum" spanning ±<see cref="bound"/> on every axis,
    /// the camera at the origin with a 60° vertical FOV, one LOD level and a
    /// prototype whose local half-extents are (0.5, 5, 0.5): the largest
    /// half-extent is 5 and the half-diagonal ≈ 5.0249.
    /// </summary>
    internal sealed class JobFixture : IDisposable
    {
        public static readonly Vector3 LOCAL_EXTENTS = new Vector3(0.5f, 5f, 0.5f);

        public readonly float bound;
        public CullAndLODJob job;

        private NativeArray<Matrix4x4> matrices;
        private NativeArray<InstanceCullResult> results;
        private NativeArray<LODFadeState> fadeStates;
        private NativeArray<int> counts;
        private NativeArray<int> offsets;
        private NativeArray<int> cursor;
        private NativeArray<int> stats;
        private NativeList<Matrix4x4> outMatrices;
        private NativeList<float4x4> outInverses;
        private NativeList<float> outFades;

        public JobFixture(float bound, params Matrix4x4[] instances)
        {
            this.bound = bound;
            int n = Mathf.Max(instances.Length, 1);
            matrices = new NativeArray<Matrix4x4>(n, Allocator.Persistent);
            for (int i = 0; i < instances.Length; i++) matrices[i] = instances[i];
            results = new NativeArray<InstanceCullResult>(n, Allocator.Persistent);
            fadeStates = new NativeArray<LODFadeState>(n, Allocator.Persistent);
            for (int i = 0; i < n; i++) fadeStates[i] = new LODFadeState { from = LODFadeState.UNSET };
            counts = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
            offsets = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
            cursor = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
            stats = new NativeArray<int>(CullAndLODJob.STAT_COUNT, Allocator.Persistent);
            outMatrices = new NativeList<Matrix4x4>(n, Allocator.Persistent);
            outInverses = new NativeList<float4x4>(n, Allocator.Persistent);
            outFades = new NativeList<float>(n, Allocator.Persistent);

            job = new CullAndLODJob
            {
                instanceCount = instances.Length,
                cameraPosition = float3.zero,
                // Inward normals for the six faces of the ±bound box.
                plane0 = new float4(-1f, 0f, 0f, bound),
                plane1 = new float4(1f, 0f, 0f, bound),
                plane2 = new float4(0f, -1f, 0f, bound),
                plane3 = new float4(0f, 1f, 0f, bound),
                plane4 = new float4(0f, 0f, -1f, bound),
                plane5 = new float4(0f, 0f, 1f, bound),
                halfFovTan = Mathf.Tan(Mathf.Deg2Rad * 30f),
                orthoHalfHeight = 0f,
                objectHalfHeight = LOCAL_EXTENTS.y,
                prototypeBoundsCenterLocal = float3.zero,
                prototypeBoundsExtentsLocal = LOCAL_EXTENTS,
                distanceCulling = true,
                cullDistance = float.MaxValue,
                minCullDistance = 0f,
                frustumCulling = true,
                frustumOffset = 0f,
                lodCount = 1,
                lodThreshold0 = 0.001f,
                lodThreshold1 = float.NegativeInfinity,
                lodThreshold2 = float.NegativeInfinity,
                lodThreshold3 = float.NegativeInfinity,
                lodBias = 1f,
                maximumLODLevel = 0,
                crossFadeMode = CullAndLODJob.CROSSFADE_NONE,
                crossFadeTransitionWidth = 0.1f,
                crossFadeAnimateSpeed = 4f,
                deltaTime = 0f,
                castShadows = false,
                shadowDistanceCulling = false,
                shadowDistance = float.MaxValue,
                minShadowCullingDistance = 0f,
                shadowFrustumCulling = false,
                shadowFrustumOffset = 0f,
                shadowLODMap = new int4(0, 1, 2, 3),
            };
        }

        /// <summary>
        /// Three LOD levels with the TestHelpers thresholds: LOD0 ≥ 0.5,
        /// LOD1 ≥ 0.1, LOD2 ≥ 0.001. With objectHalfHeight 5 and the 60°
        /// FOV, relativeHeight ≈ 8.66 / distance.
        /// </summary>
        public void UseThreeLODs()
        {
            job.lodCount = 3;
            job.lodThreshold0 = 0.5f;
            job.lodThreshold1 = 0.1f;
            job.lodThreshold2 = 0.001f;
        }

        public void SetInstance(int index, Matrix4x4 matrix) => matrices[index] = matrix;

        public void Run(float deltaTime = 0f)
        {
            job.deltaTime = deltaTime;
            job.instanceMatrices = matrices;
            job.fadeStates = fadeStates;
            job.results = results;
            job.bucketCounts = counts;
            job.bucketOffsets = offsets;
            job.bucketCursor = cursor;
            job.outMatrices = outMatrices;
            job.outInverses = outInverses;
            job.outFades = outFades;
            job.stats = stats;
            job.Execute();
        }

        public int Stat(int index) => stats[index];

        public int Count(int mode, int fadeState, int level) => counts[BucketLayout.Index(mode, fadeState, level)];

        public int TotalEntries => stats[CullAndLODJob.STAT_ENTRIES];

        public Matrix4x4 MatrixAt(int mode, int fadeState, int level, int i) =>
            outMatrices[offsets[BucketLayout.Index(mode, fadeState, level)] + i];

        public float4x4 InverseAt(int mode, int fadeState, int level, int i) =>
            outInverses[offsets[BucketLayout.Index(mode, fadeState, level)] + i];

        public float FadeAt(int mode, int fadeState, int level, int i) =>
            outFades[offsets[BucketLayout.Index(mode, fadeState, level)] + i];

        public InstanceCullResult ResultAt(int index) => results[index];

        public LODFadeState FadeStateAt(int index) => fadeStates[index];

        public void Dispose()
        {
            matrices.Dispose();
            results.Dispose();
            fadeStates.Dispose();
            counts.Dispose();
            offsets.Dispose();
            cursor.Dispose();
            stats.Dispose();
            outMatrices.Dispose();
            outInverses.Dispose();
            outFades.Dispose();
        }
    }
}
