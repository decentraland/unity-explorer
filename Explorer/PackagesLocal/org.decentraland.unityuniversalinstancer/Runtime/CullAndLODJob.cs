// Burst-compiled per-renderer cull, LOD-select and bucket-scatter job.
//
// Pass 1 visits every instance once: distance cull, frustum cull,
// shadow-caster eligibility, LOD pick and LOD cross-fade. It records an
// InstanceCullResult per instance and counts the bucket entries that
// instance emits.
// Pass 2 prefix-sums the counts into bucket offsets, sizes the flat output
// lists to the exact total and scatters each instance's matrix, inverse and
// fade factor into its buckets.
//
// A bucket is one (draw mode, fade state, LOD level) triple (BucketLayout).
// An instance emits up to four entries: a colour and a shadow entry for the
// LOD it fades out of, and the same pair for the LOD it fades into.
//
// Burst-compilable: only blittable types, no managed references.

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GPUInstancerPro
{
    /// <summary>
    /// Per-instance classification for one frame.
    /// </summary>
    internal struct InstanceCullResult
    {
        public const byte NONE = 255;
        public const byte FLAG_COLOR = 1;
        public const byte FLAG_SHADOW = 2;

        /// <summary>LOD drawn at +fade; NONE when the instance is culled.</summary>
        public byte lodOut;
        /// <summary>LOD drawn at -fade while cross-fading; NONE when steady.</summary>
        public byte lodIn;
        /// <summary>FLAG_COLOR: visible to the camera. FLAG_SHADOW: casts shadows.</summary>
        public byte flags;
        public byte pad;
        /// <summary>1 = steady, (0,1) = cross-fading.</summary>
        public float fade;
    }

    /// <summary>
    /// Animated cross-fade state, carried across frames per instance.
    /// </summary>
    internal struct LODFadeState
    {
        public const byte UNSET = 255;

        public byte from;
        public byte to;
        /// <summary>1 = settled on <see cref="to"/>.</summary>
        public float progress;
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast)]
    internal struct CullAndLODJob : IJob
    {
        public const int CROSSFADE_NONE = 0;
        public const int CROSSFADE_SPATIAL = 1;
        public const int CROSSFADE_ANIMATED = 2;

        // The dither keeps the fading-out LOD where dither < fade and the
        // fading-in LOD where dither > fade, so a factor of exactly 0 or 1
        // would leave one of the pair without pixels.
        public const float FADE_EPSILON = 1f / 256f;

        public const int STAT_DRAWN = 0;
        public const int STAT_CULLED_DISTANCE = 1;
        public const int STAT_CULLED_FRUSTUM = 2;
        public const int STAT_CULLED_LOD_SIZE = 3;
        public const int STAT_SHADOW_ONLY = 4;
        public const int STAT_CROSSFADING = 5;
        public const int STAT_ENTRIES = 6;
        // Camera-visible instances per LOD level (the level they fade out
        // of while cross-fading), BucketLayout.LEVELS entries.
        public const int STAT_LOD_HISTOGRAM = 8;
        public const int STAT_COUNT = STAT_LOD_HISTOGRAM + BucketLayout.LEVELS;

        // ---- Input ----

        [ReadOnly] public NativeArray<Matrix4x4> instanceMatrices;
        public int instanceCount;

        // Camera state. Six frustum planes packed as float4 (xyz = inward
        // unit normal, w = distance).
        public float3 cameraPosition;
        public float4 plane0;
        public float4 plane1;
        public float4 plane2;
        public float4 plane3;
        public float4 plane4;
        public float4 plane5;

        // tan(vertical fov / 2). Unity's LODGroup relative height for a
        // perspective camera is objectSize / (distance * 2 * halfFovTan);
        // with objectHalfHeight = objectSize / 2 that is
        // objectHalfHeight / (distance * halfFovTan).
        public float halfFovTan;
        // > 0 selects the orthographic model: objectHalfHeight / orthoHalfHeight.
        public float orthoHalfHeight;

        public float objectHalfHeight;
        public float3 prototypeBoundsCenterLocal;
        public float3 prototypeBoundsExtentsLocal;

        // Distance + frustum culling (GPUIProfile.isDistanceCulling,
        // minMaxDistance, minCullingDistance, isFrustumCulling, frustumOffset).
        public bool distanceCulling;
        public float cullDistance;
        public float minCullDistance;
        public bool frustumCulling;
        public float frustumOffset;

        // LOD thresholds: lodThreshold[i] = LOD i's screenRelativeTransitionHeight,
        // the relative height below which LOD i+1 takes over. Below the last
        // LOD's threshold the instance is culled.
        public int lodCount;
        public float lodThreshold0;
        public float lodThreshold1;
        public float lodThreshold2;
        public float lodThreshold3;
        public float lodBias;
        public int maximumLODLevel;

        // Cross-fade (GPUIProfile.isLODCrossFade, isAnimateCrossFade,
        // lodCrossFadeTransitionWidth, lodCrossFadeAnimateSpeed).
        public int crossFadeMode;
        public float crossFadeTransitionWidth;
        public float crossFadeAnimateSpeed;
        public float deltaTime;
        public NativeArray<LODFadeState> fadeStates;

        // Shadows (GPUIProfile.isShadowCasting, isShadowDistanceCulling,
        // minShadowCullingDistance, isShadowFrustumCulling, shadowFrustumOffset,
        // shadowLODMap). shadowDistance is the resolved caster distance.
        public bool castShadows;
        public bool shadowDistanceCulling;
        public float shadowDistance;
        public float minShadowCullingDistance;
        public bool shadowFrustumCulling;
        public float shadowFrustumOffset;
        public int4 shadowLODMap;

        // ---- Output ----

        public NativeArray<InstanceCullResult> results;
        public NativeArray<int> bucketCounts;   // BucketLayout.COUNT
        public NativeArray<int> bucketOffsets;  // BucketLayout.COUNT
        public NativeArray<int> bucketCursor;   // BucketLayout.COUNT, scratch
        public NativeList<Matrix4x4> outMatrices;
        public NativeList<float4x4> outInverses;
        public NativeList<float> outFades;
        public NativeArray<int> stats;          // STAT_COUNT

        public void Execute()
        {
            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                bucketCounts[b] = 0;
                bucketCursor[b] = 0;
            }

            int drawn = 0;
            int culledDistance = 0;
            int culledFrustum = 0;
            int culledLodSize = 0;
            int shadowOnly = 0;
            int crossFading = 0;
            int4 lodHistogram = 0;

            float effectiveBias = math.max(lodBias, 0.01f);
            float safeHalfFovTan = math.max(halfFovTan, 1e-6f);
            int lastLod = math.max(lodCount - 1, 0);
            int4 shadowMap = math.clamp(shadowLODMap, 0, lastLod);
            int minLod = math.clamp(maximumLODLevel, 0, lastLod);

            for (int i = 0; i < instanceCount; i++)
            {
                Matrix4x4 m = instanceMatrices[i];
                float3 worldPos = new float3(m.m03, m.m13, m.m23);

                var result = new InstanceCullResult
                {
                    lodOut = InstanceCullResult.NONE,
                    lodIn = InstanceCullResult.NONE,
                    fade = 1f,
                };

                float dist = math.distance(worldPos, cameraPosition);
                if (distanceCulling && (dist > cullDistance || dist < minCullDistance))
                {
                    culledDistance++;
                    ResetFade(i);
                    results[i] = result;
                    continue;
                }

                // Sphere enclosing the instance-scaled prototype AABB. The
                // radius is the box's half-diagonal: the largest single
                // half-extent leaves the corners of a non-uniformly scaled box
                // outside the sphere.
                float scaleX = math.length(new float3(m.m00, m.m10, m.m20));
                float scaleY = math.length(new float3(m.m01, m.m11, m.m21));
                float scaleZ = math.length(new float3(m.m02, m.m12, m.m22));
                float radius = math.length(new float3(
                    scaleX * prototypeBoundsExtentsLocal.x,
                    scaleY * prototypeBoundsExtentsLocal.y,
                    scaleZ * prototypeBoundsExtentsLocal.z));
                float3 worldCenter = worldPos + new float3(
                    (m.m00 * prototypeBoundsCenterLocal.x) + (m.m01 * prototypeBoundsCenterLocal.y) + (m.m02 * prototypeBoundsCenterLocal.z),
                    (m.m10 * prototypeBoundsCenterLocal.x) + (m.m11 * prototypeBoundsCenterLocal.y) + (m.m12 * prototypeBoundsCenterLocal.z),
                    (m.m20 * prototypeBoundsCenterLocal.x) + (m.m21 * prototypeBoundsCenterLocal.y) + (m.m22 * prototypeBoundsCenterLocal.z));

                bool inFrustum = !frustumCulling || !OutsideFrustum(worldCenter, radius + frustumOffset);

                // A caster inside the shadow distance keeps casting from
                // outside the view frustum: within minShadowCullingDistance
                // unconditionally, further out unless shadow-frustum culling
                // rejects it against the frustum widened by shadowFrustumOffset.
                bool castsShadow = castShadows && (!shadowDistanceCulling || dist <= shadowDistance);
                bool shadowKept = castsShadow
                                  && (inFrustum
                                      || dist <= minShadowCullingDistance
                                      || !shadowFrustumCulling
                                      || !OutsideFrustum(worldCenter, radius + shadowFrustumOffset));

                if (!inFrustum && !shadowKept)
                {
                    culledFrustum++;
                    ResetFade(i);
                    results[i] = result;
                    continue;
                }

                float instanceHalfHeight = objectHalfHeight * math.cmax(new float3(scaleX, scaleY, scaleZ));
                float relativeHeight = orthoHalfHeight > 0f
                    ? (instanceHalfHeight * effectiveBias) / math.max(orthoHalfHeight, 1e-4f)
                    : (instanceHalfHeight * effectiveBias) / math.max(dist * safeHalfFovTan, 1e-4f);

                int lod = PickLOD(relativeHeight);
                if (lod < 0)
                {
                    culledLodSize++;
                    ResetFade(i);
                    results[i] = result;
                    continue;
                }
                lod = math.max(lod, minLod);

                result.lodOut = (byte)lod;
                if (crossFadeMode == CROSSFADE_SPATIAL)
                    ApplySpatialFade(lod, relativeHeight, ref result);
                else if (crossFadeMode == CROSSFADE_ANIMATED)
                    ApplyAnimatedFade(i, lod, ref result);

                result.flags = (byte)((inFrustum ? InstanceCullResult.FLAG_COLOR : 0) | (shadowKept ? InstanceCullResult.FLAG_SHADOW : 0));
                if (inFrustum)
                {
                    drawn++;
                    lodHistogram[result.lodOut]++;
                }
                else
                {
                    shadowOnly++;
                }
                if (result.fade < 1f) crossFading++;
                results[i] = result;

                int4 buckets = 0;
                float4 fades = 0;
                int emitted = Emissions(result, shadowMap, ref buckets, ref fades);
                for (int e = 0; e < emitted; e++)
                    bucketCounts[buckets[e]]++;
            }

            int total = 0;
            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                bucketOffsets[b] = total;
                total += bucketCounts[b];
            }

            outMatrices.ResizeUninitialized(total);
            outInverses.ResizeUninitialized(total);
            outFades.ResizeUninitialized(total);

            for (int i = 0; i < instanceCount; i++)
            {
                InstanceCullResult result = results[i];
                if (result.lodOut == InstanceCullResult.NONE) continue;

                int4 buckets = 0;
                float4 fades = 0;
                int emitted = Emissions(result, shadowMap, ref buckets, ref fades);
                if (emitted == 0) continue;

                Matrix4x4 m = instanceMatrices[i];
                float4x4 inverse = math.inverse((float4x4)m);
                for (int e = 0; e < emitted; e++)
                {
                    int bucket = buckets[e];
                    int slot = bucketOffsets[bucket] + bucketCursor[bucket];
                    bucketCursor[bucket]++;
                    outMatrices[slot] = m;
                    outInverses[slot] = inverse;
                    outFades[slot] = fades[e];
                }
            }

            stats[STAT_DRAWN] = drawn;
            stats[STAT_CULLED_DISTANCE] = culledDistance;
            stats[STAT_CULLED_FRUSTUM] = culledFrustum;
            stats[STAT_CULLED_LOD_SIZE] = culledLodSize;
            stats[STAT_SHADOW_ONLY] = shadowOnly;
            stats[STAT_CROSSFADING] = crossFading;
            stats[STAT_ENTRIES] = total;
            for (int level = 0; level < BucketLayout.LEVELS; level++)
                stats[STAT_LOD_HISTOGRAM + level] = lodHistogram[level];
        }

        // Bucket entries one classified instance emits, at most four:
        // (lodOut, +fade) and (lodIn, -fade), each as a colour draw that also
        // casts, or a colour-only draw plus a shadow-only draw at the shadow
        // LOD map's level.
        internal static int Emissions(in InstanceCullResult result, int4 shadowMap, ref int4 buckets, ref float4 fades)
        {
            int n = 0;
            bool color = (result.flags & InstanceCullResult.FLAG_COLOR) != 0;
            bool shadow = (result.flags & InstanceCullResult.FLAG_SHADOW) != 0;
            int fadeState = result.fade < 1f ? BucketLayout.FADE_CROSSFADING : BucketLayout.FADE_STEADY;

            EmitLevel(result.lodOut, result.fade, color, shadow, fadeState, shadowMap, ref buckets, ref fades, ref n);
            if (result.lodIn != InstanceCullResult.NONE)
                EmitLevel(result.lodIn, -result.fade, color, shadow, fadeState, shadowMap, ref buckets, ref fades, ref n);
            return n;
        }

        private static void EmitLevel(int level, float fade, bool color, bool shadow, int fadeState, int4 shadowMap,
            ref int4 buckets, ref float4 fades, ref int n)
        {
            int shadowLevel = shadowMap[level];
            if (color)
            {
                if (shadow && shadowLevel == level)
                {
                    buckets[n] = BucketLayout.Index(BucketLayout.MODE_BOTH, fadeState, level);
                    fades[n] = fade;
                    n++;
                    return;
                }

                buckets[n] = BucketLayout.Index(BucketLayout.MODE_COLOR_ONLY, fadeState, level);
                fades[n] = fade;
                n++;
            }

            if (shadow)
            {
                buckets[n] = BucketLayout.Index(BucketLayout.MODE_SHADOW_ONLY, fadeState, shadowLevel);
                fades[n] = fade;
                n++;
            }
        }

        private int PickLOD(float relativeHeight)
        {
            if (lodCount > 0 && relativeHeight >= lodThreshold0) return 0;
            if (lodCount > 1 && relativeHeight >= lodThreshold1) return 1;
            if (lodCount > 2 && relativeHeight >= lodThreshold2) return 2;
            if (lodCount > 3 && relativeHeight >= lodThreshold3) return 3;
            return -1;
        }

        private float Threshold(int lod)
        {
            if (lod <= 0) return lodThreshold0;
            if (lod == 1) return lodThreshold1;
            if (lod == 2) return lodThreshold2;
            return lodThreshold3;
        }

        // Unity LODGroup cross-fade: the last crossFadeTransitionWidth
        // fraction of a LOD's relative-height range, adjacent to the next
        // LOD, blends into that LOD. LOD0's range tops out at 100%.
        private void ApplySpatialFade(int lod, float relativeHeight, ref InstanceCullResult result)
        {
            float lower = Threshold(lod);
            float upper = lod == 0 ? math.max(1f, lodThreshold0) : Threshold(lod - 1);
            float band = crossFadeTransitionWidth * (upper - lower);
            if (band <= 0f || relativeHeight >= lower + band) return;

            float fade = math.clamp((relativeHeight - lower) / band, FADE_EPSILON, 1f);
            if (fade >= 1f) return;

            result.fade = fade;
            result.lodIn = lod + 1 < lodCount ? (byte)(lod + 1) : InstanceCullResult.NONE;
        }

        // Time-based cross-fade: a LOD change dissolves over
        // 1 / crossFadeAnimateSpeed seconds. A target change mid-dissolve
        // back to the LOD being left reverses the dissolve; any other target
        // restarts it from the LOD being entered.
        private void ApplyAnimatedFade(int index, int lod, ref InstanceCullResult result)
        {
            LODFadeState state = fadeStates[index];
            if (state.from == LODFadeState.UNSET)
            {
                state.from = (byte)lod;
                state.to = (byte)lod;
                state.progress = 1f;
            }

            if (state.progress >= 1f)
            {
                if (lod != state.to)
                {
                    state.from = state.to;
                    state.to = (byte)lod;
                    state.progress = 0f;
                }
            }
            else if (lod != state.to)
            {
                if (lod == state.from)
                {
                    byte swap = state.from;
                    state.from = state.to;
                    state.to = swap;
                    state.progress = 1f - state.progress;
                }
                else
                {
                    state.from = state.to;
                    state.to = (byte)lod;
                    state.progress = 0f;
                }
            }

            if (state.progress < 1f)
                state.progress = math.min(1f, state.progress + (deltaTime * crossFadeAnimateSpeed));

            if (state.progress >= 1f)
            {
                state.from = state.to;
                result.lodOut = state.to;
            }
            else
            {
                result.lodOut = state.from;
                result.lodIn = state.to;
                result.fade = math.clamp(1f - state.progress, FADE_EPSILON, 1f - FADE_EPSILON);
            }

            fadeStates[index] = state;
        }

        private void ResetFade(int index)
        {
            if (crossFadeMode != CROSSFADE_ANIMATED) return;
            LODFadeState state = fadeStates[index];
            state.from = LODFadeState.UNSET;
            fadeStates[index] = state;
        }

        private bool OutsideFrustum(float3 center, float radius) =>
            SphereOutside(plane0, center, radius) ||
            SphereOutside(plane1, center, radius) ||
            SphereOutside(plane2, center, radius) ||
            SphereOutside(plane3, center, radius) ||
            SphereOutside(plane4, center, radius) ||
            SphereOutside(plane5, center, radius);

        // True if the sphere is fully on the negative side of `plane`.
        private static bool SphereOutside(float4 plane, float3 c, float r)
        {
            float signed = math.dot(plane.xyz, c) + plane.w;
            return signed < -r;
        }
    }
}
