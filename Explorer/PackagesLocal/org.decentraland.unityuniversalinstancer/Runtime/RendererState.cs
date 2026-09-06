// Per-renderer state held by TreeRendererService.
//
// Hot data lives in Unity.Collections containers so the per-frame cull job
// can read/write it from Burst-compiled code. CPU-side List<Matrix4x4>
// uploads land in `transforms` via SetTransformBufferData; the cull job
// reads from there each frame and scatters the survivors into the flat
// per-bucket output lists, which are uploaded to the GPU as-is.

using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace GPUInstancerPro
{
    /// <summary>
    /// One sub-draw within a tree prototype: a single (mesh, material,
    /// submesh) triple at a given LOD level. A single LOD level can hold
    /// multiple sub-draws — e.g. DCL trees have a trunk, dark-leaf cards,
    /// light-leaf cards, etc. all sharing the same per-instance transform.
    /// </summary>
    internal struct LODSlot
    {
        public Mesh mesh;
        public Material material;
        public int submeshIndex;
        /// <summary>
        /// Which LOD level this sub-draw belongs to (0 = highest detail).
        /// Multiple LODSlots may share the same lodLevel.
        /// </summary>
        public int lodLevel;
        /// <summary>
        /// The renderer's local-to-prototype-root matrix (and inverse). The
        /// per-instance transforms place the prototype ROOT, so any child
        /// scale/offset the prefab applies to this renderer (e.g. the 100x
        /// compensation on centimetre-authored rock FBXs) must be composed
        /// into every draw.
        /// </summary>
        public Matrix4x4 localTransform;
        public Matrix4x4 localInverseTransform;
        /// <summary>
        /// Copied from the source Renderer's GameObject so the instanced draw
        /// lands in the same camera culling mask and light-culling bucket the
        /// prefab authored.
        /// </summary>
        public int layer;
        /// <summary>
        /// Copied from the source Renderer so an authored "no shadow receive"
        /// sub-mesh keeps that flag when drawn instanced.
        /// </summary>
        public bool receiveShadows;
        /// <summary>
        /// Sub-mesh index range, the static part of this slot's indirect
        /// draw arguments.
        /// </summary>
        public uint indexCount;
        public uint indexStart;
        public uint baseVertex;
    }

    /// <summary>
    /// The GPUIProfile values a renderer draws with, resolved at registration
    /// and on every SetParameterBufferData flush.
    /// </summary>
    internal struct ProfileSnapshot
    {
        public float cullDistance;
        public float minCullDistance;
        public bool distanceCulling;
        public bool frustumCulling;
        public float frustumOffset;
        public bool castShadows;
        public bool shadowDistanceCulling;
        public float customShadowDistance;
        public float minShadowCullingDistance;
        public bool shadowFrustumCulling;
        public float shadowFrustumOffset;
        public bool shadowOcclusionCulling;
        public int4 shadowLODMap;
        public float lodBias;
        public int maximumLODLevel;
        public int crossFadeMode;
        public float crossFadeTransitionWidth;
        public float crossFadeAnimateSpeed;
        public bool occlusionCulling;
        public float occlusionOffset;
        public float occlusionOffsetSizeMultiplier;
        public int occlusionAccuracy;
        public float3 boundsOffset;

        /// <summary>
        /// Engine defaults for a renderer registered without a profile: no
        /// distance limit, frustum culling on, no shadows, no cross-fade, no
        /// occlusion culling.
        /// </summary>
        public static ProfileSnapshot Default => new ProfileSnapshot
        {
            distanceCulling = true,
            frustumCulling = true,
            shadowLODMap = new int4(0, 1, 2, 3),
            lodBias = 1f,
            crossFadeMode = CullAndLODJob.CROSSFADE_NONE,
            occlusionAccuracy = 1,
        };

        public static ProfileSnapshot From(GPUIProfile profile)
        {
            if (profile == null) return Default;

            var map = new int4(0, 1, 2, 3);
            if (profile.shadowLODMap != null)
                for (int i = 0; i < BucketLayout.LEVELS && i < profile.shadowLODMap.Length; i++)
                    map[i] = profile.shadowLODMap[i];

            int crossFade = CullAndLODJob.CROSSFADE_NONE;
            if (profile.isLODCrossFade != 0)
                crossFade = profile.isAnimateCrossFade != 0 ? CullAndLODJob.CROSSFADE_ANIMATED : CullAndLODJob.CROSSFADE_SPATIAL;

            return new ProfileSnapshot
            {
                cullDistance = profile.minMaxDistance.y,
                minCullDistance = math.max(profile.minCullingDistance, 0f),
                distanceCulling = profile.isDistanceCulling != 0,
                frustumCulling = profile.isFrustumCulling != 0,
                frustumOffset = math.max(profile.frustumOffset, 0f),
                castShadows = profile.isShadowCasting != 0,
                shadowDistanceCulling = profile.isShadowDistanceCulling != 0,
                customShadowDistance = math.max(profile.customShadowDistance, 0f),
                minShadowCullingDistance = math.max(profile.minShadowCullingDistance, 0f),
                shadowFrustumCulling = profile.isShadowFrustumCulling != 0,
                shadowFrustumOffset = math.max(profile.shadowFrustumOffset, 0f),
                shadowOcclusionCulling = profile.isShadowOcclusionCulling != 0,
                shadowLODMap = map,
                lodBias = profile.lodBiasAdjustment > 0f ? profile.lodBiasAdjustment : 1f,
                maximumLODLevel = math.max(profile.maximumLODLevel, 0),
                crossFadeMode = crossFade,
                crossFadeTransitionWidth = math.clamp(profile.lodCrossFadeTransitionWidth, 0f, 1f),
                crossFadeAnimateSpeed = math.max(profile.lodCrossFadeAnimateSpeed, 0f),
                occlusionCulling = profile.isOcclusionCulling != 0,
                occlusionOffset = math.max(profile.occlusionOffset, 0f),
                occlusionOffsetSizeMultiplier = math.max(profile.occlusionOffsetSizeMultiplier, 0f),
                occlusionAccuracy = profile.occlusionAccuracy,
                boundsOffset = math.max((float3)profile.boundsOffset, 0f),
            };
        }
    }

    /// <summary>
    /// All state owned by a single renderer-key.
    ///
    /// Native capacity is set on first SetTransformBufferData and grown on
    /// demand; GPU buffers grow to the largest frame seen. Once allocated,
    /// the per-frame path allocates nothing (REQ-028): the cull job writes
    /// into the pre-allocated scratch and output containers, and submission
    /// reads from there directly.
    /// </summary>
    internal sealed class RendererEntry : IDisposable
    {
        private const int MATRIX_STRIDE = 64;
        private const int FLOAT_STRIDE = 4;

        // Bookkeeping
        public int key;
        public Transform root;
        public GameObject prototype;
        public GPUIProfile profile;
        public ProfileSnapshot settings;

        // CPU-side transforms received via SetTransformBufferData. Unmanaged
        // so the cull job can read them without GC pressure.
        public NativeList<Matrix4x4> transforms;
        public int instanceCount;          // SetInstanceCount target
        public int capacity;               // current per-instance container sizes

        // Per-instance job scratch, capacity-sized.
        public NativeArray<InstanceCullResult> cullResults;
        public NativeArray<LODFadeState> fadeStates;

        // Job outputs. Counts/offsets/cursor are BucketLayout.COUNT long;
        // stats is CullAndLODJob.STAT_COUNT long. The flat lists hold every
        // emitted bucket entry, bucket b occupying
        // [bucketOffsets[b], bucketOffsets[b] + bucketCounts[b]).
        public NativeArray<int> bucketCounts;
        public NativeArray<int> bucketOffsets;
        public NativeArray<int> bucketCursor;
        public NativeArray<int> stats;
        public NativeList<Matrix4x4> outMatrices;
        public NativeList<float4x4> outInverses;
        public NativeList<float> outFades;

        // GPU mirror of the flat output lists; direct draws read these.
        public GraphicsBuffer uploadTransforms;
        public GraphicsBuffer uploadInverses;
        public GraphicsBuffer uploadFades;
        public int uploadCapacity;

        // Occlusion-culled compaction of the upload buffers; indirect draws
        // read these. Bucket b's survivors occupy
        // [visibleOffsets[b], visibleOffsets[b] + visibleCapacities[b]).
        public GraphicsBuffer visibleTransforms;
        public GraphicsBuffer visibleInverses;
        public GraphicsBuffer visibleFades;
        public int visibleCapacity;
        public readonly int[] visibleOffsets = new int[BucketLayout.COUNT];
        public readonly int[] visibleCapacities = new int[BucketLayout.COUNT];

        // Per-bucket survivor counters, the indirect args (one entry per
        // LOD slot × mode × fade state) and the bucket each entry reads its
        // count from.
        public GraphicsBuffer countsBuffer;
        public GraphicsBuffer argsBuffer;
        public GraphicsBuffer argBucketBuffer;

        public MaterialPropertyBlock block;

        // Occlusion stats: the input counts of the frame whose counters are
        // being read back, and the occluded total that readback produced.
        public readonly int[] readbackInputCounts = new int[BucketLayout.COUNT];
        public Action<AsyncGPUReadbackRequest> readbackCallback;
        public bool readbackPending;
        public int lastOccludedCount;
        public bool disposed;

        // Resolved render data
        public LODSlot[] lods;                  // flattened across all LOD levels
        public float[] lodLevelThresholds;      // screenRelativeTransitionHeight per LOD level (size = lodLevelCount)
        public int lodLevelCount;
        public Bounds prototypeBoundsLocal;     // prototype-local; the cull job transforms by the per-instance matrix

        /// <summary>
        /// Half the prototype's largest local-axis extent — the "object size"
        /// for screen-relative-height LOD math, mirroring what Unity's
        /// LODGroup feeds its own LOD picker.
        /// </summary>
        public float prototypeHalfHeight;

        public int ArgEntryCount => lods == null ? 0 : lods.Length * BucketLayout.ARG_ENTRIES_PER_SLOT;

        public void EnsureCapacity(int required)
        {
            if (required <= capacity && bucketCounts.IsCreated) return;
            int next = Mathf.Max(capacity, 64);
            while (next < required) next *= 2;

            if (!bucketCounts.IsCreated)
            {
                bucketCounts = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
                bucketOffsets = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
                bucketCursor = new NativeArray<int>(BucketLayout.COUNT, Allocator.Persistent);
                stats = new NativeArray<int>(CullAndLODJob.STAT_COUNT, Allocator.Persistent);
                outMatrices = new NativeList<Matrix4x4>(next, Allocator.Persistent);
                outInverses = new NativeList<float4x4>(next, Allocator.Persistent);
                outFades = new NativeList<float>(next, Allocator.Persistent);
            }

            var results = new NativeArray<InstanceCullResult>(next, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            if (cullResults.IsCreated) cullResults.Dispose();
            cullResults = results;

            var fades = new NativeArray<LODFadeState>(next, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int keep = fadeStates.IsCreated ? fadeStates.Length : 0;
            if (keep > 0) NativeArray<LODFadeState>.Copy(fadeStates, fades, keep);
            if (fadeStates.IsCreated) fadeStates.Dispose();
            fadeStates = fades;
            ResetFadeStates(keep, next - keep);

            if (transforms.IsCreated)
            {
                // Preserve existing matrices by copying native-to-native (no
                // managed round-trip): size the new list to the existing
                // length for a bulk copy, then dispose the old list.
                int oldLen = transforms.Length;
                var newList = new NativeList<Matrix4x4>(next, Allocator.Persistent);
                if (oldLen > 0)
                {
                    newList.ResizeUninitialized(oldLen);
                    NativeArray<Matrix4x4>.Copy(transforms.AsArray(), newList.AsArray(), oldLen);
                }
                transforms.Dispose();
                transforms = newList;
            }
            else
            {
                transforms = new NativeList<Matrix4x4>(next, Allocator.Persistent);
            }

            capacity = next;
        }

        /// <summary>
        /// Forgets the animated cross-fade state of [start, start + count), so
        /// those instances snap to their picked LOD on their first frame.
        /// </summary>
        public void ResetFadeStates(int start, int count)
        {
            if (!fadeStates.IsCreated) return;
            int end = Mathf.Min(start + count, fadeStates.Length);
            var unset = new LODFadeState { from = LODFadeState.UNSET };
            for (int i = Mathf.Max(start, 0); i < end; i++)
                fadeStates[i] = unset;
        }

        public void EnsureUploadCapacity(int required)
        {
            if (required <= uploadCapacity && uploadTransforms != null) return;
            int next = Mathf.Max(uploadCapacity, 32);
            while (next < required) next *= 2;

            uploadTransforms?.Release();
            uploadInverses?.Release();
            uploadFades?.Release();
            uploadTransforms = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, MATRIX_STRIDE);
            uploadInverses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, MATRIX_STRIDE);
            uploadFades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, FLOAT_STRIDE);
            uploadCapacity = next;
        }

        public void EnsureVisibleCapacity(int required)
        {
            if (required <= visibleCapacity && visibleTransforms != null) return;
            int next = Mathf.Max(visibleCapacity, 32);
            while (next < required) next *= 2;

            visibleTransforms?.Release();
            visibleInverses?.Release();
            visibleFades?.Release();
            visibleTransforms = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, MATRIX_STRIDE);
            visibleInverses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, MATRIX_STRIDE);
            visibleFades = new GraphicsBuffer(GraphicsBuffer.Target.Structured, next, FLOAT_STRIDE);
            visibleCapacity = next;
        }

        /// <summary>
        /// Allocates the counters, the indirect args and the args→bucket map
        /// once. Every args entry carries its slot's static index range and
        /// a zero instance count; the cull pass fills the counts in.
        /// </summary>
        public void EnsureIndirectBuffers()
        {
            if (argsBuffer != null || lods == null || lods.Length == 0) return;

            int entries = ArgEntryCount;
            var args = new uint[entries * BucketLayout.ARG_UINTS_PER_ENTRY];
            var argBuckets = new uint[entries];
            for (int s = 0; s < lods.Length; s++)
            {
                for (int mode = 0; mode < BucketLayout.MODES; mode++)
                for (int fadeState = 0; fadeState < BucketLayout.FADE_STATES; fadeState++)
                {
                    int e = BucketLayout.ArgEntry(s, mode, fadeState);
                    int at = e * BucketLayout.ARG_UINTS_PER_ENTRY;
                    args[at] = lods[s].indexCount;
                    args[at + 1] = 0;
                    args[at + 2] = lods[s].indexStart;
                    args[at + 3] = lods[s].baseVertex;
                    args[at + 4] = 0;
                    argBuckets[e] = (uint)BucketLayout.Index(mode, fadeState, lods[s].lodLevel);
                }
            }

            countsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, BucketLayout.COUNT, FLOAT_STRIDE);
            argsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, args.Length, FLOAT_STRIDE);
            argsBuffer.SetData(args);
            argBucketBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, entries, FLOAT_STRIDE);
            argBucketBuffer.SetData(argBuckets);
        }

        public void Dispose()
        {
            disposed = true;
            if (cullResults.IsCreated) cullResults.Dispose();
            if (fadeStates.IsCreated) fadeStates.Dispose();
            if (bucketCounts.IsCreated) bucketCounts.Dispose();
            if (bucketOffsets.IsCreated) bucketOffsets.Dispose();
            if (bucketCursor.IsCreated) bucketCursor.Dispose();
            if (stats.IsCreated) stats.Dispose();
            if (outMatrices.IsCreated) outMatrices.Dispose();
            if (outInverses.IsCreated) outInverses.Dispose();
            if (outFades.IsCreated) outFades.Dispose();
            if (transforms.IsCreated) transforms.Dispose();

            uploadTransforms?.Release();
            uploadInverses?.Release();
            uploadFades?.Release();
            uploadTransforms = uploadInverses = uploadFades = null;
            uploadCapacity = 0;

            visibleTransforms?.Release();
            visibleInverses?.Release();
            visibleFades?.Release();
            visibleTransforms = visibleInverses = visibleFades = null;
            visibleCapacity = 0;

            countsBuffer?.Release();
            argsBuffer?.Release();
            argBucketBuffer?.Release();
            countsBuffer = argsBuffer = argBucketBuffer = null;

            block = null;
            capacity = 0;
        }
    }
}
