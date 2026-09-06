// State engine + Burst-jobified rendering for GPU-instanced tree prototypes.
//
// Per-frame pipeline:
//   1. For every renderer-key with live instances, schedule a Burst
//      CullAndLODJob that distance-culls, frustum-culls, LOD-selects and
//      cross-fades the survivors, classifies shadow casters, and scatters
//      each entry's matrix, inverse and fade factor into per-bucket ranges
//      of one flat output list.
//   2. Complete the whole frame's jobs through one combined handle, so the
//      renderers run concurrently rather than one after another.
//   3. Upload each renderer's flat output once, then either emit one
//      Graphics.RenderMeshPrimitives call per non-empty (LOD slot, bucket)
//      or, with occlusion culling active, run the Hi-Z compaction pass and
//      emit Graphics.RenderMeshIndirect calls whose instance counts the GPU
//      wrote.
//
// The jobs are completed inside RenderFrame rather than carried across the
// frame boundary: the output lists and GraphicsBuffers they fill are re-used
// by the following pass, and the draws submitted below read them straight
// away.

using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GPUInstancerPro
{
    internal sealed class TreeRendererService
    {
        // ---- Runtime-tunable defaults ----

        // Fallback worldBounds size for RenderMeshPrimitives when an entry has
        // no cull distance. Set once at boot by
        // GPUICoreAPI.ApplyRuntimeSettings(...) from the authored
        // GPUIRuntimeSettings SO; the default mirrors
        // Assets/DCL/Landscape/Assets/GPUI/GPUIRuntimeSettings.asset, which
        // authors instancingBoundsSize {1000, 1000, 1000}.
        public static Vector3 DefaultWorldBoundsSize = new Vector3(1000f, 1000f, 1000f);

        private const string LOD_CROSSFADE_KEYWORD = "LOD_FADE_CROSSFADE";
        private const int MAX_PYRAMIDS = 4;

        // ---- Public stats type (test introspection) ----

        public struct LastFrameStats
        {
            public int registeredRenderers;
            public int totalInstancesAttempted;
            public int totalInstancesDrawn;
            public int drawCallCount;
            public int culledByDistance;
            public int culledByFrustum;
            /// <summary>
            /// Instances where the projected screen size fell below the
            /// last-LOD's <c>screenRelativeTransitionHeight</c>. Same
            /// semantics as Unity's LODGroup hiding the GameObject when its
            /// relative height drops below the smallest LOD threshold.
            /// </summary>
            public int culledByLODSize;
            /// <summary>
            /// Instances outside the view frustum that still cast shadows
            /// into it and are submitted as shadow-only draws.
            /// </summary>
            public int shadowOnlyInstances;
            /// <summary>Instances mid LOD cross-fade, drawn at two LOD levels.</summary>
            public int crossFadingInstances;
            /// <summary>Bucket entries uploaded (an instance can contribute several).</summary>
            public int bucketEntries;
            /// <summary>
            /// Colour-draw instances the Hi-Z pass rejected, from the most
            /// recently completed asynchronous counter readback.
            /// </summary>
            public int culledByOcclusion;
            /// <summary>The frame's colour draws went through the Hi-Z pass.</summary>
            public bool occlusionCullingActive;
            public int[] lodHistogram; // length 8, only first lodCount entries populated

            public static LastFrameStats Empty() => new LastFrameStats { lodHistogram = new int[8] };
        }

        // ---- State ----

        private int nextKey;
        private readonly Dictionary<int, RendererEntry> entries = new Dictionary<int, RendererEntry>();
        private readonly Dictionary<GPUIProfile, List<int>> profileBindings = new Dictionary<GPUIProfile, List<int>>();

        // Per-frame stats (accessible across multiple cameras' RenderFrame calls
        // — last-camera-wins, mirrors what tests expect).
        private LastFrameStats _lastFrameStats = LastFrameStats.Empty();
        public LastFrameStats Stats => _lastFrameStats;

        /// <summary>
        /// Union of the Unity layers the registered prototypes draw on. A
        /// camera whose culling mask misses all of them renders none of this
        /// service's output, so it must not be allowed to drive a frame.
        /// </summary>
        public int DrawLayerMask { get; private set; }

        // Reusable plane buffer.
        private readonly Plane[] _scratchPlanes = new Plane[6];

        // Per-frame scratch, reused so the render path stays allocation-free.
        private readonly List<RendererEntry> _pending = new List<RendererEntry>();
        private NativeList<JobHandle> _jobHandles;

        // Staging array for List -> NativeList uploads. Shared by every entry
        // and grown monotonically to the largest single upload.
        private Matrix4x4[] _uploadScratch;

        // Source material -> clone with LOD_FADE_CROSSFADE enabled, used for
        // the cross-fading draws only so steady instances skip the dither.
        private readonly Dictionary<Material, Material> fadeMaterials = new Dictionary<Material, Material>();

        // One depth pyramid per driving camera.
        private readonly Dictionary<Camera, HiZDepthPyramid> pyramids = new Dictionary<Camera, HiZDepthPyramid>();

        private static readonly int GPUI_TRANSFORM_BUFFER_ID = Shader.PropertyToID("gpuiTransformBuffer");
        private static readonly int GPUI_INVERSE_TRANSFORM_BUFFER_ID = Shader.PropertyToID("gpuiInverseTransformBuffer");
        private static readonly int GPUI_LOD_FADE_BUFFER_ID = Shader.PropertyToID("gpuiLODFadeBuffer");
        private static readonly int GPUI_INSTANCE_OFFSET_ID = Shader.PropertyToID("gpuiInstanceOffset");
        private static readonly int GPUI_PROTOTYPE_LOCAL_TRANSFORM_ID = Shader.PropertyToID("gpuiPrototypeLocalTransform");
        private static readonly int GPUI_PROTOTYPE_LOCAL_INVERSE_TRANSFORM_ID = Shader.PropertyToID("gpuiPrototypeLocalInverseTransform");

        // ---- Registration (REQ-001..005) ----

        public int Register(Transform root, GameObject prototype, GPUIProfile profile)
        {
            int key = nextKey++;
            var localBounds = ComputePrototypeLocalBounds(prototype);
            var entry = new RendererEntry
            {
                key = key,
                root = root,
                prototype = prototype,
                profile = profile,
                settings = ProfileSnapshot.From(profile),
                instanceCount = 0,
                prototypeBoundsLocal = localBounds,
                prototypeHalfHeight = math.cmax((float3)localBounds.extents),
            };
            ExtractLODSlots(prototype, entry);
            entries[key] = entry;
            RefreshDrawLayerMask();

            if (profile == null)
                Debug.LogWarning($"[GPUInstancerPro] Renderer {key} registered without a GPUIProfile: cull distance, shadow casting, frustum margin and LOD bias stay at engine defaults.");

            if (entry.lodLevelCount == 0)
                Debug.LogWarning($"[GPUInstancerPro] Renderer {key} registered with {(prototype == null ? "a null prototype" : $"prototype '{prototype.name}', which has no renderer carrying both a mesh and a material")}: it will never draw.");

            if (profile != null)
            {
                if (!profileBindings.TryGetValue(profile, out var bound))
                {
                    bound = new List<int>();
                    profileBindings[profile] = bound;
                }
                bound.Add(key);
            }

            return key;
        }

        /// <summary>
        /// Releases every renderer's native and GPU buffers, the fade
        /// material clones and the depth pyramids. Safe to call more than
        /// once; the service is empty and re-usable afterwards.
        /// </summary>
        public void DisposeAll()
        {
            foreach (var kvp in entries)
                kvp.Value.Dispose();

            entries.Clear();
            profileBindings.Clear();
            _pending.Clear();
            if (_jobHandles.IsCreated) _jobHandles.Dispose();
            _uploadScratch = null;

            foreach (var kvp in fadeMaterials)
                if (kvp.Value != null) Object.DestroyImmediate(kvp.Value);
            fadeMaterials.Clear();

            foreach (var kvp in pyramids)
                kvp.Value.Dispose();
            pyramids.Clear();

            DrawLayerMask = 0;
            _lastFrameStats = LastFrameStats.Empty();
        }

        // Walks the prototype's LODGroup and flattens every (renderer, sub-mesh)
        // pair into LODSlot entries tagged with their LOD level. A single LOD
        // level can contribute multiple LODSlots — DCL trees commonly have
        // 4-6 sub-renderers per LOD level (trunk, dark/light leaves, glow card,
        // etc.). They all share the same per-instance transform; the cull job
        // only needs one threshold per level.
        private static void ExtractLODSlots(GameObject prototype, RendererEntry entry)
        {
            entry.lods = System.Array.Empty<LODSlot>();
            entry.lodLevelThresholds = System.Array.Empty<float>();
            entry.lodLevelCount = 0;
            if (prototype == null) return;

            var lodGroup = prototype.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                // No LODGroup — treat every child renderer as the single LOD0
                // sub-draw set.
                var slots = new List<LODSlot>(4);
                CollectRendererSlots(prototype, /*lodLevel*/ 0, slots);
                if (slots.Count == 0) return;
                entry.lods = slots.ToArray();
                entry.lodLevelThresholds = new[] { 0f };
                entry.lodLevelCount = 1;
                return;
            }

            var lods = lodGroup.GetLODs();
            int levelCount = math.min(lods.Length, BucketLayout.LEVELS);
            var allSlots = new List<LODSlot>(lods.Length * 4);
            var thresholds = new List<float>(levelCount);
            for (int level = 0; level < levelCount; level++)
            {
                if (lods[level].renderers == null) continue;

                int slotsBefore = allSlots.Count;
                // Levels that turn out empty are dropped, so a kept level's
                // index is however many levels have been kept so far.
                int keptLevel = thresholds.Count;

                foreach (var rend in lods[level].renderers)
                {
                    if (rend == null) continue;
                    var mf = rend.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    var mats = rend.sharedMaterials;
                    Matrix4x4 local = RootRelativeMatrix(prototype.transform, rend.transform);
                    int meshSubmeshCount = mf.sharedMesh.subMeshCount;
                    for (int sm = 0; sm < meshSubmeshCount; sm++)
                    {
                        Material mat = (sm < mats.Length) ? mats[sm] : null;
                        if (mat == null) continue;
                        allSlots.Add(MakeSlot(mf.sharedMesh, mat, sm, keptLevel, local, rend));
                    }
                }

                // A level with no drawable sub-mesh must not enter the
                // threshold table: the cull job would bucket instances into it
                // and count them drawn while nothing gets submitted.
                if (allSlots.Count > slotsBefore)
                    thresholds.Add(lods[level].screenRelativeTransitionHeight);
            }

            entry.lods = allSlots.ToArray();
            entry.lodLevelThresholds = thresholds.ToArray();
            entry.lodLevelCount = thresholds.Count;
        }

        private static void CollectRendererSlots(GameObject prototype, int lodLevel, List<LODSlot> sink)
        {
            var renderers = prototype.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                var mf = rend.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mats = rend.sharedMaterials;
                Matrix4x4 local = RootRelativeMatrix(prototype.transform, rend.transform);
                int subMeshCount = mf.sharedMesh.subMeshCount;
                for (int sm = 0; sm < subMeshCount; sm++)
                {
                    Material mat = (sm < mats.Length) ? mats[sm] : null;
                    if (mat == null) continue;
                    sink.Add(MakeSlot(mf.sharedMesh, mat, sm, lodLevel, local, rend));
                }
            }
        }

        private static LODSlot MakeSlot(Mesh mesh, Material material, int submesh, int lodLevel, Matrix4x4 local, Renderer rend) =>
            new LODSlot
            {
                mesh = mesh,
                material = material,
                submeshIndex = submesh,
                lodLevel = lodLevel,
                localTransform = local,
                localInverseTransform = local.inverse,
                layer = rend.gameObject.layer,
                receiveShadows = rend.receiveShadows,
                indexCount = mesh.GetIndexCount(submesh),
                indexStart = mesh.GetIndexStart(submesh),
                baseVertex = mesh.GetBaseVertex(submesh),
            };

        private void RefreshDrawLayerMask()
        {
            int mask = 0;
            foreach (var kvp in entries)
            {
                var lods = kvp.Value.lods;
                if (lods == null) continue;
                for (int i = 0; i < lods.Length; i++)
                    mask |= 1 << lods[i].layer;
            }
            DrawLayerMask = mask;
        }

        // Renderer-local -> prototype-root. Prefab assets keep consistent
        // localToWorld matrices within the asset, so this also works on
        // prototypes that are not instantiated in a scene.
        private static Matrix4x4 RootRelativeMatrix(Transform root, Transform renderer) =>
            renderer == root
                ? Matrix4x4.identity
                : root.worldToLocalMatrix * renderer.localToWorldMatrix;

        private static Bounds ComputePrototypeLocalBounds(GameObject prototype)
        {
            if (prototype == null) return new Bounds(Vector3.zero, Vector3.one);
            var renderers = prototype.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one);
            // We want bounds in prototype-ROOT space because the cull job multiplies
            // by the per-instance (root) matrix. Each mesh's local bounds must pass
            // through its renderer's root-relative matrix — prefabs may scale their
            // renderer children (e.g. 100x on centimetre-authored rock FBXs).
            Bounds b = new Bounds();
            bool any = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                var mf = renderers[i].GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                Matrix4x4 local = RootRelativeMatrix(prototype.transform, renderers[i].transform);
                Bounds lb = TransformBounds(local, mf.sharedMesh.bounds);
                if (!any) { b = lb; any = true; }
                else b.Encapsulate(lb);
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one);
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            Vector3 min = bounds.min, max = bounds.max;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? min.x : max.x,
                    (i & 2) == 0 ? min.y : max.y,
                    (i & 4) == 0 ? min.z : max.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return result;
        }

        // ---- Instance count (REQ-006/007/024) ----

        public void SetInstanceCount(int rendererKey, int count)
        {
            if (!entries.TryGetValue(rendererKey, out var entry)) return;
            if (count < 0) count = 0;
            entry.instanceCount = count;
        }

        public int GetInstanceCount(int rendererKey)
        {
            return entries.TryGetValue(rendererKey, out var entry) ? entry.instanceCount : 0;
        }

        // ---- Transform buffer (REQ-008..012/025) ----

        public void SetTransformBufferData(int rendererKey, List<Matrix4x4> matrices, int bufferStart, int matricesStart, int count)
        {
            if (!entries.TryGetValue(rendererKey, out var entry)) return;
            if (matrices == null || count <= 0) return;
            if (matricesStart < 0 || matricesStart + count > matrices.Count) return;
            if (bufferStart < 0) return;

            int required = bufferStart + count;
            entry.EnsureCapacity(required);

            // Grow the NativeList up to required length. Only the gap the
            // upload skipped needs a defined value — [bufferStart, required)
            // is overwritten by the copy below.
            int oldLen = entry.transforms.Length;
            if (oldLen < required)
            {
                entry.transforms.ResizeUninitialized(required);
                for (int i = oldLen; i < bufferStart; i++)
                    entry.transforms[i] = Matrix4x4.identity;
            }

            // Two bulk moves (List -> staging -> native) rather than a
            // per-element round trip through the NativeList indexer: a realm
            // switch pushes every instance of a prototype in a single call.
            if (_uploadScratch == null || _uploadScratch.Length < count)
                _uploadScratch = new Matrix4x4[Mathf.NextPowerOfTwo(count)];
            matrices.CopyTo(matricesStart, _uploadScratch, 0, count);
            NativeArray<Matrix4x4>.Copy(_uploadScratch, 0, entry.transforms.AsArray(), bufferStart, count);

            // Re-uploaded slots are fresh instances: their LOD snaps rather
            // than dissolving from whatever the slot held before.
            entry.ResetFadeStates(bufferStart, count);
        }

        public Matrix4x4 GetMatrix(int rendererKey, int slot)
        {
            if (!entries.TryGetValue(rendererKey, out var entry)) return Matrix4x4.identity;
            if (slot < 0 || slot >= entry.transforms.Length) return Matrix4x4.identity;
            return entry.transforms[slot];
        }

        // ---- Profile flush (REQ-013/015) ----

        public void OnProfileFlushed(GPUIProfile profile)
        {
            if (profile == null) return;
            if (!profileBindings.TryGetValue(profile, out var bound)) return;
            ProfileSnapshot settings = ProfileSnapshot.From(profile);
            for (int i = 0; i < bound.Count; i++)
            {
                if (entries.TryGetValue(bound[i], out var entry))
                    entry.settings = settings;
            }
        }

        public float GetCullDistance(int rendererKey)
        {
            return entries.TryGetValue(rendererKey, out var entry) ? entry.settings.cullDistance : 0f;
        }

        public ProfileSnapshot GetSettings(int rendererKey)
        {
            return entries.TryGetValue(rendererKey, out var entry) ? entry.settings : ProfileSnapshot.Default;
        }

        public int FadeMaterialCount => fadeMaterials.Count;

        // ---- Render (REQ-014/018/021/023) ----

        public void RenderFrame(Camera camera, float deltaTime)
        {
            if (camera == null) return;

            if (_lastFrameStats.lodHistogram == null) _lastFrameStats = LastFrameStats.Empty();
            else System.Array.Clear(_lastFrameStats.lodHistogram, 0, _lastFrameStats.lodHistogram.Length);
            _lastFrameStats.registeredRenderers = entries.Count;
            _lastFrameStats.totalInstancesAttempted = 0;
            _lastFrameStats.totalInstancesDrawn = 0;
            _lastFrameStats.drawCallCount = 0;
            _lastFrameStats.culledByDistance = 0;
            _lastFrameStats.culledByFrustum = 0;
            _lastFrameStats.culledByLODSize = 0;
            _lastFrameStats.shadowOnlyInstances = 0;
            _lastFrameStats.crossFadingInstances = 0;
            _lastFrameStats.bucketEntries = 0;
            _lastFrameStats.culledByOcclusion = 0;
            _lastFrameStats.occlusionCullingActive = false;

            // Camera frustum planes — pack to float4 for the Burst job.
            GeometryUtility.CalculateFrustumPlanes(camera, _scratchPlanes);
            float4 p0 = PackPlane(_scratchPlanes[0]);
            float4 p1 = PackPlane(_scratchPlanes[1]);
            float4 p2 = PackPlane(_scratchPlanes[2]);
            float4 p3 = PackPlane(_scratchPlanes[3]);
            float4 p4 = PackPlane(_scratchPlanes[4]);
            float4 p5 = PackPlane(_scratchPlanes[5]);
            float3 camPos = camera.transform.position;
            float halfFovTanY = math.tan(math.radians(camera.fieldOfView) * 0.5f);
            float orthoHalfHeight = camera.orthographic ? camera.orthographicSize : 0f;

            // Unity's own LODGroup scales screen-relative height by the active
            // quality level's LOD bias. Read it per frame so a quality change
            // takes effect without re-registering the prototypes.
            float qualityLodBias = QualitySettings.lodBias;
            float pipelineShadowDistance = PipelineShadowDistance(camera);
            bool occlusionSupported = OcclusionCulling.Supported;
            HiZDepthPyramid pyramid = null;

            _pending.Clear();
            if (!_jobHandles.IsCreated) _jobHandles = new NativeList<JobHandle>(8, Allocator.Persistent);
            _jobHandles.Clear();

            foreach (var kvp in entries)
            {
                var entry = kvp.Value;
                if (entry.instanceCount <= 0) continue;
                if (entry.lods == null || entry.lods.Length == 0) continue;
                int lodCount = entry.lodLevelCount;
                if (lodCount == 0) continue;
                if (!entry.transforms.IsCreated) continue;
                int liveCount = math.min(entry.instanceCount, entry.transforms.Length);
                if (liveCount <= 0) continue;

                _lastFrameStats.totalInstancesAttempted += liveCount;

                // Registration may have predated any SetTransformBufferData call.
                if (!entry.bucketCounts.IsCreated || entry.cullResults.Length < liveCount)
                    entry.EnsureCapacity(math.max(liveCount, 64));

                ProfileSnapshot settings = entry.settings;
                if (occlusionSupported && settings.occlusionCulling && pyramid == null)
                    pyramid = GetPyramid(camera);

                // Pull per-LOD threshold values for the job — defaults to
                // -inf for missing LODs so the picker never matches them.
                float t0 = lodCount > 0 ? entry.lodLevelThresholds[0] : float.NegativeInfinity;
                float t1 = lodCount > 1 ? entry.lodLevelThresholds[1] : float.NegativeInfinity;
                float t2 = lodCount > 2 ? entry.lodLevelThresholds[2] : float.NegativeInfinity;
                float t3 = lodCount > 3 ? entry.lodLevelThresholds[3] : float.NegativeInfinity;

                var job = new CullAndLODJob
                {
                    instanceMatrices = entry.transforms.AsArray(),
                    instanceCount = liveCount,
                    cameraPosition = camPos,
                    plane0 = p0, plane1 = p1, plane2 = p2, plane3 = p3, plane4 = p4, plane5 = p5,
                    halfFovTan = halfFovTanY,
                    orthoHalfHeight = orthoHalfHeight,
                    objectHalfHeight = entry.prototypeHalfHeight,
                    prototypeBoundsCenterLocal = entry.prototypeBoundsLocal.center,
                    prototypeBoundsExtentsLocal = (float3)entry.prototypeBoundsLocal.extents + settings.boundsOffset,
                    distanceCulling = settings.distanceCulling,
                    cullDistance = settings.cullDistance > 0f ? settings.cullDistance : float.MaxValue,
                    minCullDistance = settings.minCullDistance,
                    frustumCulling = settings.frustumCulling,
                    frustumOffset = settings.frustumOffset,
                    lodCount = lodCount,
                    lodThreshold0 = t0, lodThreshold1 = t1, lodThreshold2 = t2, lodThreshold3 = t3,
                    lodBias = settings.lodBias * qualityLodBias,
                    maximumLODLevel = settings.maximumLODLevel,
                    crossFadeMode = settings.crossFadeMode,
                    crossFadeTransitionWidth = settings.crossFadeTransitionWidth,
                    crossFadeAnimateSpeed = settings.crossFadeAnimateSpeed,
                    deltaTime = deltaTime,
                    fadeStates = entry.fadeStates,
                    castShadows = settings.castShadows,
                    shadowDistanceCulling = settings.shadowDistanceCulling,
                    shadowDistance = settings.customShadowDistance > 0f ? settings.customShadowDistance : pipelineShadowDistance,
                    minShadowCullingDistance = settings.minShadowCullingDistance,
                    shadowFrustumCulling = settings.shadowFrustumCulling,
                    shadowFrustumOffset = settings.shadowFrustumOffset,
                    shadowLODMap = settings.shadowLODMap,
                    results = entry.cullResults,
                    bucketCounts = entry.bucketCounts,
                    bucketOffsets = entry.bucketOffsets,
                    bucketCursor = entry.bucketCursor,
                    outMatrices = entry.outMatrices,
                    outInverses = entry.outInverses,
                    outFades = entry.outFades,
                    stats = entry.stats,
                };

                _jobHandles.Add(job.Schedule());
                _pending.Add(entry);
            }

            if (pyramid != null)
            {
                pyramid.Enqueue(camera);
                _lastFrameStats.occlusionCullingActive = pyramid.ReadyForCulling;
            }

            if (_jobHandles.Length > 0)
                JobHandle.CombineDependencies(_jobHandles.AsArray()).Complete();

            for (int e = 0; e < _pending.Count; e++)
                UploadAndSubmit(_pending[e], camera, pyramid, camPos);
        }

        // Shadow casters beyond the pipeline's shadow distance never reach a
        // shadow map; GPUIProfile.customShadowDistance overrides it per profile.
        private static float PipelineShadowDistance(Camera camera)
        {
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            float distance = asset != null ? asset.shadowDistance : QualitySettings.shadowDistance;
            return Mathf.Min(distance, camera.farClipPlane);
        }

        private HiZDepthPyramid GetPyramid(Camera camera)
        {
            if (pyramids.TryGetValue(camera, out HiZDepthPyramid pyramid)) return pyramid;

            if (pyramids.Count >= MAX_PYRAMIDS)
            {
                foreach (var kvp in pyramids)
                    kvp.Value.Dispose();
                pyramids.Clear();
            }

            pyramid = new HiZDepthPyramid();
            pyramids[camera] = pyramid;
            return pyramid;
        }

        private Material FadeMaterial(Material source)
        {
            if (fadeMaterials.TryGetValue(source, out Material clone) && clone != null) return clone;

            clone = new Material(source)
            {
                name = source.name + " (LOD cross-fade)",
                hideFlags = HideFlags.HideAndDontSave,
            };
            clone.EnableKeyword(LOD_CROSSFADE_KEYWORD);
            fadeMaterials[source] = clone;
            return clone;
        }

        private static ShadowCastingMode ShadowModeFor(int mode)
        {
            if (mode == BucketLayout.MODE_BOTH) return ShadowCastingMode.On;
            return mode == BucketLayout.MODE_SHADOW_ONLY ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off;
        }

        // Drains one renderer's completed cull job: uploads the flat output
        // lists once, then emits one draw per non-empty (LOD slot, bucket).
        //
        // Procedural instancing (RenderMeshPrimitives / RenderMeshIndirect) is
        // required because the production tree shaders are authored with
        //   #pragma instancing_options procedural:setupGPUI
        // so they fetch their per-instance world matrix from a
        // StructuredBuffer<float4x4> ('gpuiTransformBuffer') indexed by
        // SV_InstanceID plus the draw's offset, rather than from the
        // per-instance constant block Graphics.RenderMeshInstanced fills.
        // Every draw of the entry shares one property block; RenderMesh*
        // snapshots matProps at call time, so per-draw values are safe.
        private void UploadAndSubmit(RendererEntry entry, Camera camera, HiZDepthPyramid pyramid, float3 cameraPosition)
        {
            int levels = math.min(entry.lodLevelCount, BucketLayout.LEVELS);

            _lastFrameStats.totalInstancesDrawn += entry.stats[CullAndLODJob.STAT_DRAWN];
            _lastFrameStats.culledByDistance += entry.stats[CullAndLODJob.STAT_CULLED_DISTANCE];
            _lastFrameStats.culledByFrustum += entry.stats[CullAndLODJob.STAT_CULLED_FRUSTUM];
            _lastFrameStats.culledByLODSize += entry.stats[CullAndLODJob.STAT_CULLED_LOD_SIZE];
            _lastFrameStats.shadowOnlyInstances += entry.stats[CullAndLODJob.STAT_SHADOW_ONLY];
            _lastFrameStats.crossFadingInstances += entry.stats[CullAndLODJob.STAT_CROSSFADING];
            for (int level = 0; level < levels; level++)
                _lastFrameStats.lodHistogram[level] += entry.stats[CullAndLODJob.STAT_LOD_HISTOGRAM + level];

            int total = entry.stats[CullAndLODJob.STAT_ENTRIES];
            _lastFrameStats.bucketEntries += total;
            if (total == 0) return;

            entry.EnsureUploadCapacity(total);
            entry.uploadTransforms.SetData(entry.outMatrices.AsArray(), 0, 0, total);
            entry.uploadInverses.SetData(entry.outInverses.AsArray(), 0, 0, total);
            entry.uploadFades.SetData(entry.outFades.AsArray(), 0, 0, total);

            bool useOcclusion = pyramid != null && pyramid.ReadyForCulling && entry.settings.occlusionCulling;
            if (useOcclusion)
            {
                int visibleTotal = OcclusionMath.ComputeVisibleLayout(entry.bucketCounts, !entry.settings.shadowOcclusionCulling,
                    entry.visibleOffsets, entry.visibleCapacities);
                entry.EnsureVisibleCapacity(visibleTotal);
                entry.EnsureIndirectBuffers();
                OcclusionCulling.Dispatch(entry, pyramid, cameraPosition);
                OcclusionCulling.RequestStats(entry);
                _lastFrameStats.culledByOcclusion += entry.lastOccludedCount;
            }

            var block = entry.block ?? (entry.block = new MaterialPropertyBlock());
            block.SetBuffer(GPUI_TRANSFORM_BUFFER_ID, useOcclusion ? entry.visibleTransforms : entry.uploadTransforms);
            block.SetBuffer(GPUI_INVERSE_TRANSFORM_BUFFER_ID, useOcclusion ? entry.visibleInverses : entry.uploadInverses);
            block.SetBuffer(GPUI_LOD_FADE_BUFFER_ID, useOcclusion ? entry.visibleFades : entry.uploadFades);

            float cullDistance = entry.settings.cullDistance;
            var worldBounds = new Bounds(
                camera.transform.position,
                cullDistance > 0 ? Vector3.one * (cullDistance * 4f) : DefaultWorldBoundsSize);

            for (int s = 0; s < entry.lods.Length; s++)
            {
                var slot = entry.lods[s];
                int level = slot.lodLevel;
                if (level < 0 || level >= levels) continue;
                if (slot.mesh == null || slot.material == null) continue;

                for (int mode = 0; mode < BucketLayout.MODES; mode++)
                for (int fadeState = 0; fadeState < BucketLayout.FADE_STATES; fadeState++)
                {
                    int bucket = BucketLayout.Index(mode, fadeState, level);
                    int count = useOcclusion ? entry.visibleCapacities[bucket] : entry.bucketCounts[bucket];
                    if (count == 0) continue;

                    Material material = fadeState == BucketLayout.FADE_CROSSFADING ? FadeMaterial(slot.material) : slot.material;
                    block.SetMatrix(GPUI_PROTOTYPE_LOCAL_TRANSFORM_ID, slot.localTransform);
                    block.SetMatrix(GPUI_PROTOTYPE_LOCAL_INVERSE_TRANSFORM_ID, slot.localInverseTransform);
                    block.SetInteger(GPUI_INSTANCE_OFFSET_ID, useOcclusion ? entry.visibleOffsets[bucket] : entry.bucketOffsets[bucket]);

                    var rparams = new RenderParams(material)
                    {
                        layer = slot.layer,
                        renderingLayerMask = RenderingLayerMask.defaultRenderingLayerMask,
                        rendererPriority = 0,
                        camera = camera,
                        receiveShadows = slot.receiveShadows,
                        shadowCastingMode = ShadowModeFor(mode),
                        worldBounds = worldBounds,
                        matProps = block,
                    };

                    if (useOcclusion)
                        Graphics.RenderMeshIndirect(rparams, slot.mesh, entry.argsBuffer, 1, BucketLayout.ArgEntry(s, mode, fadeState));
                    else
                        Graphics.RenderMeshPrimitives(rparams, slot.mesh, slot.submeshIndex, count);
                    _lastFrameStats.drawCallCount++;
                }
            }
        }

        private static float4 PackPlane(Plane p) =>
            new float4(p.normal.x, p.normal.y, p.normal.z, p.distance);
    }
}
