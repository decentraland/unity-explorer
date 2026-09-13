// Hi-Z occlusion culling: a per-camera depth pyramid rebuilt by a render
// pass after the opaques of every driven frame, and the compute dispatch
// that tests each bucket of instances against the pyramid captured by the
// preceding frame, compacting the survivors for indirect draws.

using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace GPUInstancerPro
{
    /// <summary>
    /// Pure helpers shared by the C# side and mirrored by
    /// GPUIOcclusionCulling.compute.
    /// </summary>
    internal static class OcclusionMath
    {
        public const int MAX_PYRAMID_SIZE = 1024;
        public const int MIN_ACCURACY = 1;
        public const int MAX_ACCURACY = 4;

        public static int FloorPow2(int value)
        {
            int p = 1;
            while (p * 2 <= value) p *= 2;
            return p;
        }

        /// <summary>
        /// Largest power-of-two pyramid at or below the render size, capped
        /// at <see cref="MAX_PYRAMID_SIZE"/> per axis.
        /// </summary>
        public static int2 PyramidSize(int width, int height) =>
            new int2(FloorPow2(math.clamp(width, 1, MAX_PYRAMID_SIZE)), FloorPow2(math.clamp(height, 1, MAX_PYRAMID_SIZE)));

        public static int MipCount(int2 size) => 1 + (int)math.floor(math.log2(math.max(size.x, size.y)));

        /// <summary>
        /// Texels per axis an instance's screen rect is tested against;
        /// GPUIProfile.occlusionAccuracy picks the pyramid level.
        /// </summary>
        public static int Footprint(int accuracy) => 1 << math.clamp(accuracy, MIN_ACCURACY, MAX_ACCURACY);

        public static int SelectMip(float extentTexels, int footprint, int maxMip)
        {
            if (extentTexels <= footprint) return 0;
            return math.clamp((int)math.ceil(math.log2(extentTexels / footprint)), 0, maxMip);
        }

        /// <summary>
        /// (scale, offset) mapping NDC z onto the pyramid's depth range: 0 at
        /// the near plane, 1 at the far plane.
        /// </summary>
        public static float2 DepthParams(bool reversedZ, bool openGLClipSpace)
        {
            if (reversedZ) return new float2(-1f, 1f);
            return openGLClipSpace ? new float2(0.5f, 0.5f) : new float2(1f, 0f);
        }

        public static float NormalizeBufferDepth(float raw, bool reversedZ) => reversedZ ? 1f - raw : raw;

        public static bool IsOccluded(float nearDepth, float farthestOccluderDepth, float bias) =>
            nearDepth - bias > farthestOccluderDepth;

        public static bool OpenGLClipSpace(GraphicsDeviceType type) =>
            type == GraphicsDeviceType.OpenGLCore || type == GraphicsDeviceType.OpenGLES3;

        /// <summary>
        /// Lays out the compacted (visible) buffer: bucket b's survivors get
        /// [offsets[b], offsets[b] + capacities[b]). A shadow-only bucket also
        /// has room for the casters demoted from the both-passes bucket of the
        /// same level and fade state. Returns the total length.
        /// </summary>
        public static int ComputeVisibleLayout(NativeArray<int> counts, bool demoteOccludedCasters, int[] offsets, int[] capacities)
        {
            int total = 0;
            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                int capacity = counts[b];
                if (demoteOccludedCasters && BucketLayout.Mode(b) == BucketLayout.MODE_SHADOW_ONLY)
                    capacity += counts[BucketLayout.Index(BucketLayout.MODE_BOTH, BucketLayout.FadeState(b), BucketLayout.Level(b))];
                capacities[b] = capacity;
                offsets[b] = total;
                total += capacity;
            }
            return total;
        }
    }

    internal static class OcclusionCulling
    {
        public const string COMPUTE_RESOURCE = "GPUIOcclusionCulling";
        private const int CULL_THREADS = 64;
        private const int ARGS_THREADS = 64;

        private static readonly int ID_HIZ = Shader.PropertyToID("_HiZ");
        private static readonly int ID_IN_TRANSFORMS = Shader.PropertyToID("_InTransforms");
        private static readonly int ID_IN_INVERSES = Shader.PropertyToID("_InInverses");
        private static readonly int ID_IN_FADES = Shader.PropertyToID("_InFades");
        private static readonly int ID_OUT_TRANSFORMS = Shader.PropertyToID("_OutTransforms");
        private static readonly int ID_OUT_INVERSES = Shader.PropertyToID("_OutInverses");
        private static readonly int ID_OUT_FADES = Shader.PropertyToID("_OutFades");
        private static readonly int ID_COUNTS = Shader.PropertyToID("_Counts");
        private static readonly int ID_ARG_BUCKET = Shader.PropertyToID("_ArgBucket");
        private static readonly int ID_ARGS = Shader.PropertyToID("_Args");
        private static readonly int ID_VIEW_PROJ = Shader.PropertyToID("_ViewProj");
        private static readonly int ID_CAMERA_POSITION = Shader.PropertyToID("_CameraPosition");
        private static readonly int ID_PYRAMID_PARAMS = Shader.PropertyToID("_PyramidParams");
        private static readonly int ID_DEPTH_PARAMS = Shader.PropertyToID("_DepthParams");
        private static readonly int ID_BOUNDS_CENTER = Shader.PropertyToID("_BoundsCenter");
        private static readonly int ID_BOUNDS_EXTENTS = Shader.PropertyToID("_BoundsExtents");
        private static readonly int ID_BUCKET_PARAMS = Shader.PropertyToID("_BucketParams");
        private static readonly int ID_DEMOTE_PARAMS = Shader.PropertyToID("_DemoteParams");
        private static readonly int ID_ARG_COUNT = Shader.PropertyToID("_ArgCount");

        private static readonly int[] intScratch = new int[4];
        private static readonly uint[] zeroCounts = new uint[BucketLayout.COUNT];

        private static ComputeShader shader;
        private static bool shaderResolved;
        private static int kernelCull;
        private static int kernelArgs;

        public static ComputeShader ComputeShader
        {
            get
            {
                if (!shaderResolved)
                {
                    shaderResolved = true;
                    shader = Resources.Load<ComputeShader>(COMPUTE_RESOURCE);
                    if (shader != null)
                    {
                        kernelCull = shader.FindKernel("CullInstances");
                        kernelArgs = shader.FindKernel("WriteArgs");
                    }
                }
                return shader;
            }
        }

        /// <summary>
        /// A graphics device with compute support, URP as the active pipeline
        /// (the depth pyramid pass is a URP render pass) and the packaged
        /// compute shader.
        /// </summary>
        public static bool Supported =>
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null
            && SystemInfo.supportsComputeShaders
            && UniversalRenderPipeline.asset != null
            && ComputeShader != null;

        /// <summary>
        /// Tests every non-empty bucket of <paramref name="entry"/> against the
        /// pyramid, compacting survivors into the visible buffers at the
        /// entry's visible layout, then writes each bucket's survivor count
        /// into its indirect args. Shadow-only buckets are tested only with
        /// shadow occlusion culling on; otherwise occluded casters from a
        /// both-passes bucket are demoted to the shadow-only bucket of the same
        /// level and fade state.
        /// </summary>
        public static void Dispatch(RendererEntry entry, HiZDepthPyramid pyramid, float3 cameraPosition)
        {
            ComputeShader cs = ComputeShader;
            ProfileSnapshot settings = entry.settings;
            bool demoteOccludedCasters = !settings.shadowOcclusionCulling;

            entry.countsBuffer.SetData(zeroCounts);

            cs.SetMatrix(ID_VIEW_PROJ, pyramid.ViewProj);
            cs.SetVector(ID_CAMERA_POSITION, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 1f));
            cs.SetVector(ID_PYRAMID_PARAMS, new Vector4(pyramid.Width, pyramid.Height, pyramid.MipCount - 1, OcclusionMath.Footprint(settings.occlusionAccuracy)));
            float2 depthParams = OcclusionMath.DepthParams(SystemInfo.usesReversedZBuffer, OcclusionMath.OpenGLClipSpace(SystemInfo.graphicsDeviceType));
            cs.SetVector(ID_DEPTH_PARAMS, new Vector4(depthParams.x, depthParams.y, settings.occlusionOffset, settings.occlusionOffsetSizeMultiplier));
            cs.SetVector(ID_BOUNDS_CENTER, entry.prototypeBoundsLocal.center);
            cs.SetVector(ID_BOUNDS_EXTENTS, entry.prototypeBoundsLocal.extents + (Vector3)settings.boundsOffset);

            cs.SetTexture(kernelCull, ID_HIZ, pyramid.Texture);
            cs.SetBuffer(kernelCull, ID_IN_TRANSFORMS, entry.uploadTransforms);
            cs.SetBuffer(kernelCull, ID_IN_INVERSES, entry.uploadInverses);
            cs.SetBuffer(kernelCull, ID_IN_FADES, entry.uploadFades);
            cs.SetBuffer(kernelCull, ID_OUT_TRANSFORMS, entry.visibleTransforms);
            cs.SetBuffer(kernelCull, ID_OUT_INVERSES, entry.visibleInverses);
            cs.SetBuffer(kernelCull, ID_OUT_FADES, entry.visibleFades);
            cs.SetBuffer(kernelCull, ID_COUNTS, entry.countsBuffer);

            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                int count = entry.bucketCounts[b];
                if (count == 0) continue;

                int mode = BucketLayout.Mode(b);
                bool test = mode != BucketLayout.MODE_SHADOW_ONLY || settings.shadowOcclusionCulling;
                int demoteBucket = -1;
                int demoteOffset = 0;
                if (mode == BucketLayout.MODE_BOTH && demoteOccludedCasters)
                {
                    demoteBucket = BucketLayout.Index(BucketLayout.MODE_SHADOW_ONLY, BucketLayout.FadeState(b), BucketLayout.Level(b));
                    demoteOffset = entry.visibleOffsets[demoteBucket];
                }

                intScratch[0] = entry.bucketOffsets[b];
                intScratch[1] = count;
                intScratch[2] = b;
                intScratch[3] = entry.visibleOffsets[b];
                cs.SetInts(ID_BUCKET_PARAMS, intScratch);
                intScratch[0] = demoteBucket;
                intScratch[1] = demoteOffset;
                intScratch[2] = test ? 1 : 0;
                intScratch[3] = 0;
                cs.SetInts(ID_DEMOTE_PARAMS, intScratch);
                cs.Dispatch(kernelCull, (count + CULL_THREADS - 1) / CULL_THREADS, 1, 1);
            }

            int argEntries = entry.ArgEntryCount;
            cs.SetInt(ID_ARG_COUNT, argEntries);
            cs.SetBuffer(kernelArgs, ID_COUNTS, entry.countsBuffer);
            cs.SetBuffer(kernelArgs, ID_ARG_BUCKET, entry.argBucketBuffer);
            cs.SetBuffer(kernelArgs, ID_ARGS, entry.argsBuffer);
            cs.Dispatch(kernelArgs, (argEntries + ARGS_THREADS - 1) / ARGS_THREADS, 1, 1);
        }

        /// <summary>
        /// Reads the survivor counters back asynchronously; one request per
        /// entry is in flight at a time. The occluded total is the input
        /// count minus the survivors over the colour buckets.
        /// </summary>
        public static void RequestStats(RendererEntry entry)
        {
            if (entry.readbackPending || !SystemInfo.supportsAsyncGPUReadback) return;
            for (int b = 0; b < BucketLayout.COUNT; b++)
                entry.readbackInputCounts[b] = entry.bucketCounts[b];
            entry.readbackPending = true;
            entry.readbackCallback ??= request => OnStatsReadback(entry, request);
            AsyncGPUReadback.Request(entry.countsBuffer, entry.readbackCallback);
        }

        private static void OnStatsReadback(RendererEntry entry, AsyncGPUReadbackRequest request)
        {
            entry.readbackPending = false;
            if (entry.disposed || request.hasError) return;

            NativeArray<uint> survivors = request.GetData<uint>();
            int occluded = 0;
            for (int b = 0; b < BucketLayout.COUNT; b++)
            {
                if (BucketLayout.Mode(b) == BucketLayout.MODE_SHADOW_ONLY) continue;
                occluded += entry.readbackInputCounts[b] - (int)survivors[b];
            }
            entry.lastOccludedCount = Mathf.Max(occluded, 0);
        }
    }

    /// <summary>
    /// Depth pyramid of one camera. Mip 0 is the largest power-of-two
    /// downsample of the camera depth, every mip holds the farthest depth of
    /// its 2×2 footprint, so a texel at any level is the farthest occluder
    /// over the screen area it covers.
    /// </summary>
    internal sealed class HiZDepthPyramid : IDisposable
    {
        private const int REDUCE_THREADS = 8;

        private static readonly int ID_SOURCE_DEPTH = Shader.PropertyToID("_SourceDepth");
        private static readonly int ID_HIZ_DST = Shader.PropertyToID("_HiZDst");
        private static readonly int ID_HIZ_SRC = Shader.PropertyToID("_HiZSrc");
        private static readonly int ID_SIZE = Shader.PropertyToID("_Size");
        private static readonly int ID_REVERSED_Z = Shader.PropertyToID("_ReversedZ");

        private readonly DepthPyramidPass pass;
        private readonly int[] sizeScratch = new int[4];
        private RenderTexture texture;
        private RTHandle handle;
        private int kernelCopy;
        private int kernelReduce;
        private bool kernelsResolved;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public int MipCount { get; private set; }

        /// <summary>World → clip of the frame whose depth the pyramid holds.</summary>
        public Matrix4x4 ViewProj { get; private set; }
        public bool Captured { get; private set; }
        public int CapturedFrame { get; private set; }

        public RenderTexture Texture => texture;
        public bool ReadyForCulling => Captured && texture != null;

        public HiZDepthPyramid()
        {
            pass = new DepthPyramidPass(this);
        }

        /// <summary>
        /// Queues the pyramid rebuild on the camera's renderer for the frame
        /// being started.
        /// </summary>
        public void Enqueue(Camera camera)
        {
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            ScriptableRenderer renderer = cameraData.scriptableRenderer;
            if (renderer != null) renderer.EnqueuePass(pass);
        }

        internal bool EnsureTexture(int renderWidth, int renderHeight)
        {
            if (renderWidth <= 0 || renderHeight <= 0) return false;
            int2 size = OcclusionMath.PyramidSize(renderWidth, renderHeight);
            if (texture != null && size.x == Width && size.y == Height) return true;

            ReleaseTexture();
            var descriptor = new RenderTextureDescriptor(size.x, size.y, RenderTextureFormat.RFloat, 0)
            {
                enableRandomWrite = true,
                useMipMap = true,
                autoGenerateMips = false,
                msaaSamples = 1,
                sRGB = false,
            };
            texture = new RenderTexture(descriptor)
            {
                name = "GPUI Hi-Z",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            handle = RTHandles.Alloc(texture);
            Width = size.x;
            Height = size.y;
            MipCount = OcclusionMath.MipCount(size);
            Captured = false;
            return true;
        }

        internal void Build(UnsafeCommandBuffer cmd, RenderTargetIdentifier sourceDepth, int sourceWidth, int sourceHeight, Matrix4x4 viewProj)
        {
            ComputeShader cs = OcclusionCulling.ComputeShader;
            if (cs == null || texture == null) return;
            if (!kernelsResolved)
            {
                kernelCopy = cs.FindKernel("CopyDepth");
                kernelReduce = cs.FindKernel("ReduceDepth");
                kernelsResolved = true;
            }

            cmd.SetComputeFloatParam(cs, ID_REVERSED_Z, SystemInfo.usesReversedZBuffer ? 1f : 0f);
            sizeScratch[0] = sourceWidth;
            sizeScratch[1] = sourceHeight;
            sizeScratch[2] = Width;
            sizeScratch[3] = Height;
            cmd.SetComputeIntParams(cs, ID_SIZE, sizeScratch);
            cmd.SetComputeTextureParam(cs, kernelCopy, ID_SOURCE_DEPTH, sourceDepth);
            cmd.SetComputeTextureParam(cs, kernelCopy, ID_HIZ_DST, texture, 0);
            cmd.DispatchCompute(cs, kernelCopy, Groups(Width), Groups(Height), 1);

            int width = Width;
            int height = Height;
            for (int mip = 1; mip < MipCount; mip++)
            {
                int nextWidth = Mathf.Max(1, width / 2);
                int nextHeight = Mathf.Max(1, height / 2);
                sizeScratch[0] = width;
                sizeScratch[1] = height;
                sizeScratch[2] = nextWidth;
                sizeScratch[3] = nextHeight;
                cmd.SetComputeIntParams(cs, ID_SIZE, sizeScratch);
                cmd.SetComputeTextureParam(cs, kernelReduce, ID_HIZ_SRC, texture, mip - 1);
                cmd.SetComputeTextureParam(cs, kernelReduce, ID_HIZ_DST, texture, mip);
                cmd.DispatchCompute(cs, kernelReduce, Groups(nextWidth), Groups(nextHeight), 1);
                width = nextWidth;
                height = nextHeight;
            }

            ViewProj = viewProj;
            Captured = true;
            CapturedFrame = Time.frameCount;
        }

        private static int Groups(int size) => (size + REDUCE_THREADS - 1) / REDUCE_THREADS;

        private void ReleaseTexture()
        {
            handle?.Release();
            handle = null;
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            }
            Captured = false;
        }

        public void Dispose() => ReleaseTexture();

        // Rebuilds the pyramid from the camera depth texture once the opaques
        // of the frame are in, capturing that frame's view-projection with it.
        private sealed class DepthPyramidPass : ScriptableRenderPass
        {
            private readonly HiZDepthPyramid owner;

            private class PassData
            {
                public HiZDepthPyramid owner;
                public TextureHandle depth;
                public int sourceWidth;
                public int sourceHeight;
                public Matrix4x4 viewProj;
            }

            public DepthPyramidPass(HiZDepthPyramid owner)
            {
                this.owner = owner;
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                profilingSampler = new ProfilingSampler("GPUI Hi-Z pyramid");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                TextureHandle depth = resources.cameraDepthTexture;
                if (!depth.IsValid()) return;

                var cameraData = frameData.Get<UniversalCameraData>();
                RenderTextureDescriptor target = cameraData.cameraTargetDescriptor;
                if (!owner.EnsureTexture(target.width, target.height)) return;

                Camera camera = cameraData.camera;
                TextureHandle pyramid = renderGraph.ImportTexture(owner.handle);
                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("GPUI Hi-Z pyramid", out PassData data, profilingSampler))
                {
                    data.owner = owner;
                    data.depth = depth;
                    data.sourceWidth = target.width;
                    data.sourceHeight = target.height;
                    data.viewProj = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix;
                    builder.UseTexture(depth, AccessFlags.Read);
                    builder.UseTexture(pyramid, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (PassData d, UnsafeGraphContext context) =>
                        d.owner.Build(context.cmd, d.depth, d.sourceWidth, d.sourceHeight, d.viewProj));
                }
            }
        }
    }
}
