using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    /// <summary>
    ///     The always-on stylized water surface is rendered by the ocean material
    ///     (Decentraland/StylizedOcean) on the OceanProceduralMesh tiles — it owns
    ///     the waves, depth colour, refraction, reflection and foam. This render
    ///     feature adds three optional sub-passes that enrich that surface when their
    ///     gate is enabled (all off by default in ForwardRenderer-High.asset):
    ///
    ///       * displacement pre-pass  — bakes a wide-area swell heightfield the water
    ///         vertex stage samples, so the swell rolls across tiles uniformly.
    ///       * directional caustics   — projects animated caustics onto submerged
    ///         surfaces (reconstructed from scene depth), rippling with the sun.
    ///       * screen-space reflection— renders a planar reflection texture the water
    ///         blends against the sky probe by Fresnel.
    ///
    ///     Each enabled sub-pass publishes its result through global shader
    ///     properties the water shader reads; when disabled the globals stay at 0 and
    ///     the water falls back to its self-contained path, so enqueuing nothing is
    ///     the zero-cost default.
    /// </summary>
    public class RendererFeature_Ocean : ScriptableRendererFeature
    {
        [Serializable]
        public class ScreenSpaceReflectionSettings
        {
            [SerializeField] public bool enable;
        }

        [Serializable]
        public class DisplacementPrePassSettings
        {
            [SerializeField] public bool enable;
            [SerializeField] public float range = 500f;
            [SerializeField] public float cellSize = 0.25f;
        }

        [SerializeField] private ScreenSpaceReflectionSettings screenSpaceReflectionSettings = new ();
        [SerializeField] private bool directionalCaustics;
        [SerializeField] private DisplacementPrePassSettings displacementPrePassSettings = new ();

        // The renderer asset binds the sub-pass shaders here; those references are
        // what carry them into player builds, where nothing else references them
        // and Shader.Find would come back empty.
        [SerializeField] private Shader causticsShader;
        [SerializeField] private Shader ssrShader;
        [SerializeField] private Shader displacementShader;

        private const string CAUSTICS_SHADER = "Hidden/DCL/RenderFeatures/OceanCaustics";
        private const string SSR_SHADER = "Hidden/DCL/RenderFeatures/OceanSSR";
        private const string DISPLACEMENT_SHADER = "Hidden/DCL/RenderFeatures/OceanDisplacement";

        private static readonly int s_DisplacementTexId = Shader.PropertyToID("_OceanDisplacementTex");
        private static readonly int s_DisplacementParamsId = Shader.PropertyToID("_OceanDisplacementParams");
        private static readonly int s_ScreenReflectionTexId = Shader.PropertyToID("_OceanScreenReflectionTex");
        private static readonly int s_ScreenReflectionParamsId = Shader.PropertyToID("_OceanScreenReflectionParams");
        private static readonly int s_DispCenterId = Shader.PropertyToID("_OceanDispCenter");
        private static readonly int s_DispSizeId = Shader.PropertyToID("_OceanDispSize");

        private Material causticsMaterial;
        private Material ssrMaterial;
        private Material displacementMaterial;

        private RenderTexture displacementRT;
        private RTHandle displacementHandle;
        private RTHandle ssrHandle;

        private CausticsPass causticsPass;
        private DisplacementPass displacementPass;
        private ScreenReflectionPass ssrPass;

        private bool displacementPublished;
        private bool ssrPublished;

        public override void Create()
        {
            // Unity re-runs Create on every domain reload and on every inspector edit
            // of the renderer asset; release what the previous run owned first.
            Dispose(true);

            name = "RendererFeature_Ocean";

            // Every sub-pass is gated off in the shipped renderer asset, so nothing
            // is built until its gate is on.
            if (displacementPrePassSettings.enable)
            {
                displacementMaterial = TryCreateMaterial(displacementShader, DISPLACEMENT_SHADER);

                if (displacementMaterial != null)
                {
                    float range = Mathf.Max(1f, displacementPrePassSettings.range);
                    int dim = Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.RoundToInt(range / Mathf.Max(0.01f, displacementPrePassSettings.cellSize))), 64, 512);
                    displacementRT = new RenderTexture(dim, dim, 0, GraphicsFormat.R16_SFloat)
                    {
                        name = "_DCL_OceanDisplacement",
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Bilinear,
                        useMipMap = false,
                        hideFlags = HideFlags.HideAndDontSave,
                    };
                    displacementRT.Create();
                    displacementHandle = RTHandles.Alloc(displacementRT);

                    displacementMaterial.SetVector(s_DispCenterId, Vector4.zero);
                    displacementMaterial.SetFloat(s_DispSizeId, range);

                    displacementPass = new DisplacementPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingOpaques };
                }
            }

            if (directionalCaustics)
            {
                causticsMaterial = TryCreateMaterial(causticsShader, CAUSTICS_SHADER);

                if (causticsMaterial != null)
                    causticsPass = new CausticsPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingOpaques };
            }

            if (screenSpaceReflectionSettings.enable)
            {
                ssrMaterial = TryCreateMaterial(ssrShader, SSR_SHADER);

                if (ssrMaterial != null)
                    ssrPass = new ScreenReflectionPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraType camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection)
                return;

            if (displacementPrePassSettings.enable && displacementPass != null)
            {
                renderer.EnqueuePass(displacementPass);
                displacementPublished = true;
            }
            else
                ClearDisplacementGlobals();

            if (directionalCaustics && causticsPass != null)
                renderer.EnqueuePass(causticsPass);

            if (screenSpaceReflectionSettings.enable && ssrPass != null)
            {
                renderer.EnqueuePass(ssrPass);
                ssrPublished = true;
            }
            else
                ClearScreenReflectionGlobals();
        }

        protected override void Dispose(bool disposing)
        {
            ClearDisplacementGlobals();
            ClearScreenReflectionGlobals();

            if (displacementHandle != null) { RTHandles.Release(displacementHandle); displacementHandle = null; }
            if (displacementRT != null) { displacementRT.Release(); CoreUtils.Destroy(displacementRT); displacementRT = null; }
            if (ssrHandle != null) { RTHandles.Release(ssrHandle); ssrHandle = null; }

            CoreUtils.Destroy(causticsMaterial);
            CoreUtils.Destroy(ssrMaterial);
            CoreUtils.Destroy(displacementMaterial);
            causticsMaterial = ssrMaterial = displacementMaterial = null;
            causticsPass = null;
            displacementPass = null;
            ssrPass = null;
        }

        // The water shader keeps sampling a published global until it is zeroed, so
        // a gate that stops feeding one has to retract it, not just teardown.
        private void ClearDisplacementGlobals()
        {
            if (!displacementPublished) return;

            Shader.SetGlobalVector(s_DisplacementParamsId, Vector4.zero);
            displacementPublished = false;
        }

        private void ClearScreenReflectionGlobals()
        {
            if (!ssrPublished) return;

            Shader.SetGlobalVector(s_ScreenReflectionParamsId, Vector4.zero);
            ssrPublished = false;
        }

        private static Material TryCreateMaterial(Shader authored, string shaderName)
        {
            Shader shader = authored != null ? authored : Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[RendererFeature_Ocean] shader '{shaderName}' not found; that sub-pass is unavailable.");
                return null;
            }
            return CoreUtils.CreateEngineMaterial(shader);
        }

        // -------------------------------------------------------------
        // Displacement pre-pass: bake the swell heightfield into a global.
        // -------------------------------------------------------------
        private sealed class DisplacementPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ("DCL.Ocean.Displacement");
            private readonly RendererFeature_Ocean feature;

            internal DisplacementPass(RendererFeature_Ocean f) { feature = f; profilingSampler = s_Sampler; }

            private sealed class PassData
            {
                internal Material Material;
                internal TextureHandle Target;
                internal Vector2 Center;
                internal float InvSize;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (feature.displacementMaterial == null || feature.displacementHandle == null)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // The baked region is a finite window around the viewer; snapping its
                // centre to the cell grid keeps the sampled heightfield stable while
                // the camera moves instead of pinning the swell to the world origin.
                float cell = Mathf.Max(0.01f, feature.displacementPrePassSettings.cellSize);
                Vector3 cameraPosition = cameraData.camera.transform.position;
                var center = new Vector2(Mathf.Floor(cameraPosition.x / cell) * cell,
                                         Mathf.Floor(cameraPosition.z / cell) * cell);

                feature.displacementMaterial.SetVector(s_DispCenterId, new Vector4(center.x, center.y, 0f, 0f));

                TextureHandle target = renderGraph.ImportTexture(feature.displacementHandle);

                using var builder = renderGraph.AddUnsafePass<PassData>("DCL.Ocean.Displacement", out PassData data, s_Sampler);
                data.Material = feature.displacementMaterial;
                data.Target = target;
                data.Center = center;
                data.InvSize = 1f / Mathf.Max(1f, feature.displacementPrePassSettings.range);

                builder.UseTexture(target, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData d, UnsafeGraphContext ctx) =>
                {
                    UnsafeCommandBuffer cmd = ctx.cmd;
                    cmd.SetRenderTarget(d.Target);
                    cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3, 1);
                    cmd.SetGlobalTexture(s_DisplacementTexId, d.Target);
                    cmd.SetGlobalVector(s_DisplacementParamsId, new Vector4(d.Center.x, d.Center.y, d.InvSize, 1f));
                });
            }
        }

        // -------------------------------------------------------------
        // Caustics: additive projection onto submerged surfaces.
        // -------------------------------------------------------------
        private sealed class CausticsPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ("DCL.Ocean.Caustics");
            private readonly RendererFeature_Ocean feature;

            internal CausticsPass(RendererFeature_Ocean f) { feature = f; profilingSampler = s_Sampler; }

            private sealed class PassData
            {
                internal Material Material;
                internal TextureHandle Color;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (feature.causticsMaterial == null)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle color = resourceData.activeColorTexture;
                if (!color.IsValid())
                    return;

                using var builder = renderGraph.AddUnsafePass<PassData>("DCL.Ocean.Caustics", out PassData data, s_Sampler);
                data.Material = feature.causticsMaterial;
                data.Color = color;

                // Additive over what is already in the camera colour.
                builder.UseTexture(color, AccessFlags.ReadWrite);

                // The shader reconstructs world position from _CameraDepthTexture and
                // reads the URP light globals; both have to be declared or the graph
                // may cull their producers or hand this pass stale bindings.
                builder.UseAllGlobalTextures(true);
                if (resourceData.cameraDepthTexture.IsValid())
                    builder.UseTexture(resourceData.cameraDepthTexture);

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData d, UnsafeGraphContext ctx) =>
                {
                    UnsafeCommandBuffer cmd = ctx.cmd;
                    cmd.SetRenderTarget(d.Color);
                    cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3, 1);
                });
            }
        }

        // -------------------------------------------------------------
        // Screen-space planar reflection into a global reflection texture.
        // -------------------------------------------------------------
        private sealed class ScreenReflectionPass : ScriptableRenderPass
        {
            private static readonly ProfilingSampler s_Sampler = new ("DCL.Ocean.SSR");
            private readonly RendererFeature_Ocean feature;

            internal ScreenReflectionPass(RendererFeature_Ocean f) { feature = f; profilingSampler = s_Sampler; }

            private sealed class PassData
            {
                internal Material Material;
                internal TextureHandle Target;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (feature.ssrMaterial == null)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;
                RenderingUtils.ReAllocateHandleIfNeeded(ref feature.ssrHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_DCL_OceanScreenReflection");

                TextureHandle target = renderGraph.ImportTexture(feature.ssrHandle);

                using var builder = renderGraph.AddUnsafePass<PassData>("DCL.Ocean.SSR", out PassData data, s_Sampler);
                data.Material = feature.ssrMaterial;
                data.Target = target;

                builder.UseTexture(target, AccessFlags.Write);

                // The shader mirrors _CameraOpaqueTexture; declare it (and the rest of
                // the URP globals it reads through the shader library) explicitly.
                builder.UseAllGlobalTextures(true);
                if (resourceData.cameraOpaqueTexture.IsValid())
                    builder.UseTexture(resourceData.cameraOpaqueTexture);

                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData d, UnsafeGraphContext ctx) =>
                {
                    UnsafeCommandBuffer cmd = ctx.cmd;
                    cmd.SetRenderTarget(d.Target);
                    cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3, 1);
                    cmd.SetGlobalTexture(s_ScreenReflectionTexId, d.Target);
                    cmd.SetGlobalVector(s_ScreenReflectionParamsId, new Vector4(0.8f, 0f, 0f, 0f));
                });
            }
        }
    }
}
