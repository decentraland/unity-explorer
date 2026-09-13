using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    /// <summary>
    ///     Draws a coloured outline/glow around objects the gameplay layer flags as
    ///     highlighted. Two independent buckets are exposed:
    ///     <see cref="HighlightedObjects"/> (scene objects, driven by
    ///     InteractionHighlightSystem) and <see cref="HighlightedObjects_Avatar"/>
    ///     (avatars, driven by AvatarHighlightSystem). Both persist across frames —
    ///     gameplay calls <c>Highlight</c> on hover-enter/update and <c>Disparage</c>
    ///     on hover-exit.
    ///
    ///     The pass renders every flagged renderer into a premultiplied silhouette
    ///     mask (<see cref="highlightInputMaterial"/>), blurs it separably into a glow
    ///     (<see cref="highlightInputBlurMaterial"/>), then additively composites the
    ///     rim over the camera colour (<see cref="highlightOutputMaterial"/>). When
    ///     both buckets are empty nothing is enqueued, so the feature is free.
    /// </summary>
    public class RenderFeature_ObjectHighlight : ScriptableRendererFeature
    {
        // ---------- persistent per-object highlight buckets ----------

        internal readonly struct HighlightEntry
        {
            internal readonly Color Color;
            internal readonly float Intensity;
            internal readonly int SubMeshCount;

            internal HighlightEntry(Color color, float intensity, int subMeshCount)
            {
                Color = color;
                Intensity = intensity;
                SubMeshCount = subMeshCount;
            }
        }

        /// <summary>
        ///     A bucket of tracked highlights, keyed by <see cref="Renderer"/> so
        ///     repeated <c>Highlight</c> calls overwrite cleanly. Two instances are
        ///     kept static so gameplay code can reach them without a feature handle.
        /// </summary>
        public abstract class HighlightBucket
        {
            internal readonly Dictionary<Renderer, HighlightEntry> Entries = new (64);

            internal void HighlightImpl(IEnumerable<Renderer> renderers, Color color, float intensity)
            {
                if (renderers == null) return;

                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    Entries[r] = new HighlightEntry(color, intensity, ResolveSubMeshCount(r));
                }
            }

            internal void DisparageImpl(IEnumerable<Renderer> renderers)
            {
                if (renderers == null) return;

                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    Entries.Remove(r);
                }
            }

            // DrawRenderer errors out on a submesh index past the mesh's range, so
            // the draw count is clamped against the geometry as well as the material
            // array. Resolved on insert: the mesh does not change while highlighted.
            private static int ResolveSubMeshCount(Renderer renderer)
            {
                if (renderer is SkinnedMeshRenderer skinned)
                    return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;

                if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                    return filter.sharedMesh.subMeshCount;

                return 1;
            }
        }

        private sealed class GeneralBucket : HighlightBucket { }
        private sealed class AvatarBucket : HighlightBucket { }

        private static readonly GeneralBucket s_GeneralBucket = new ();
        private static readonly AvatarBucket s_AvatarBucket = new ();

        /// <summary>Scene-object highlights (InteractionHighlightSystem).</summary>
        public static class HighlightedObjects
        {
            public static void Highlight(IEnumerable<Renderer> renderers, Color color, float intensity) =>
                s_GeneralBucket.HighlightImpl(renderers, color, intensity);

            public static void Disparage(IEnumerable<Renderer> renderers) =>
                s_GeneralBucket.DisparageImpl(renderers);
        }

        /// <summary>Avatar highlights (AvatarHighlightSystem). Colour alpha carries the fade opacity.</summary>
        public static class HighlightedObjects_Avatar
        {
            public static void Highlight(IEnumerable<Renderer> renderers, Color color, float intensity) =>
                s_AvatarBucket.HighlightImpl(renderers, color, intensity);

            public static void Disparage(IEnumerable<Renderer> renderers) =>
                s_AvatarBucket.DisparageImpl(renderers);
        }

        // ---------- serialized interface (bound by ForwardRenderer-High.asset) ----------

        [SerializeField] public Material highlightInputMaterial;
        [SerializeField] public Material highlightInputBlurMaterial;
        [SerializeField] public Material highlightOutputMaterial;

        [Range(1f, 16f)]
        [SerializeField] public float outlineThicknessScale = 1.5f;

        [SerializeField] public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingTransparents;

        // ---------- runtime state ----------

        private const string MASK_SHADER = "Hidden/DCL/RenderFeatures/HighlightMask";
        private const string BLUR_SHADER = "Hidden/DCL/RenderFeatures/HighlightBlur";
        private const string COMPOSITE_SHADER = "Hidden/DCL/RenderFeatures/HighlightComposite";

        private Material maskMaterial;
        private Material blurMaterial;
        private Material compositeMaterial;
        private bool ownsMask, ownsBlur, ownsComposite;

        private HighlightGlowPass pass;

        public override void Create()
        {
            // Unity re-runs Create on every domain reload and on every inspector
            // edit of the renderer asset; release what the previous run owned first.
            Dispose(true);

            name = "RenderFeature_ObjectHighlight";

            maskMaterial = ResolveMaterial(highlightInputMaterial, MASK_SHADER, out ownsMask);
            blurMaterial = ResolveMaterial(highlightInputBlurMaterial, BLUR_SHADER, out ownsBlur);
            compositeMaterial = ResolveMaterial(highlightOutputMaterial, COMPOSITE_SHADER, out ownsComposite);

            if (maskMaterial == null || blurMaterial == null || compositeMaterial == null)
                return;

            pass = new HighlightGlowPass(maskMaterial, blurMaterial, compositeMaterial, s_GeneralBucket, s_AvatarBucket);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null) return;

            CameraType camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Preview || camType == CameraType.Reflection)
                return;

            if (s_GeneralBucket.Entries.Count == 0 && s_AvatarBucket.Entries.Count == 0)
                return;

            pass.renderPassEvent = injectionPoint;
            pass.SetThicknessScale(outlineThicknessScale);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            if (ownsMask) CoreUtils.Destroy(maskMaterial);
            if (ownsBlur) CoreUtils.Destroy(blurMaterial);
            if (ownsComposite) CoreUtils.Destroy(compositeMaterial);
            ownsMask = ownsBlur = ownsComposite = false;
            maskMaterial = blurMaterial = compositeMaterial = null;
            pass = null;
        }

        // Prefer the material authored on the renderer asset; fall back to a
        // freshly-built engine material so the feature still works when enqueued
        // by a renderer asset that leaves the slot empty (e.g. edit-mode tests).
        private static Material ResolveMaterial(Material authored, string shaderName, out bool owned)
        {
            if (authored != null)
            {
                owned = false;
                return authored;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[RenderFeature_ObjectHighlight] shader '{shaderName}' not found; highlight disabled.");
                owned = false;
                return null;
            }

            owned = true;
            return CoreUtils.CreateEngineMaterial(shader);
        }

        // -------------------------------------------------------------
        // Glow pass: mask -> separable blur -> additive rim composite.
        // -------------------------------------------------------------

        private sealed class HighlightGlowPass : ScriptableRenderPass
        {
            private static readonly int s_HighlightColorId = Shader.PropertyToID("_HighlightColor");
            private static readonly int s_MaskTexId = Shader.PropertyToID("_HighlightMaskTex");
            private static readonly int s_BlurSrcId = Shader.PropertyToID("_HighlightBlurSrc");
            private static readonly int s_BlurTexId = Shader.PropertyToID("_HighlightBlurTex");
            private static readonly int s_BlurRadiusId = Shader.PropertyToID("_BlurRadius");

            private static readonly ProfilingSampler s_Sampler = new ("DCL.ObjectHighlight");

            private readonly Material maskMaterial;
            private readonly Material blurMaterial;
            private readonly Material compositeMaterial;
            private readonly HighlightBucket generalBucket;
            private readonly HighlightBucket avatarBucket;

            // Reused across frames so the per-frame gather does not allocate.
            private RendererTint[] scratch = new RendererTint[64];
            private readonly List<Renderer> deadKeys = new (16);
            private readonly List<Material> sharedMaterials = new (8);
            private float thicknessScale = 1.5f;

            internal HighlightGlowPass(Material mask, Material blur, Material composite,
                HighlightBucket general, HighlightBucket avatar)
            {
                maskMaterial = mask;
                blurMaterial = blur;
                compositeMaterial = composite;
                generalBucket = general;
                avatarBucket = avatar;
                profilingSampler = s_Sampler;
            }

            internal void SetThicknessScale(float s) => thicknessScale = s;

            private struct RendererTint
            {
                internal Renderer Renderer;
                internal Color Color;
                internal int Draws;
            }

            private sealed class MaskPassData
            {
                internal RendererTint[] Entries;
                internal int Count;
                internal Material Material;
                internal TextureHandle Mask;
            }

            private sealed class BlurPassData
            {
                internal Material Material;
                internal int ShaderPass;
                internal float Radius;
                internal TextureHandle Source;
                internal TextureHandle Target;
            }

            private sealed class CompositePassData
            {
                internal Material Material;
                internal TextureHandle Mask;
                internal TextureHandle Blur;
                internal TextureHandle Target;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                int total = generalBucket.Entries.Count + avatarBucket.Entries.Count;
                if (total == 0) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                TextureHandle cameraColor = resourceData.activeColorTexture;
                if (!cameraColor.IsValid()) return;

                // Gather both buckets into the reusable scratch array (also computes
                // the widest requested thickness → blur radius).
                if (scratch.Length < total)
                    scratch = new RendererTint[Mathf.NextPowerOfTwo(total)];

                int count = 0;
                float maxIntensity = 1f;
                GatherBucket(generalBucket, ref count, ref maxIntensity);
                GatherBucket(avatarBucket, ref count, ref maxIntensity);
                if (count == 0) return;

                float blurRadius = Mathf.Clamp(maxIntensity * thicknessScale, 1f, 24f);

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.depthStencilFormat = GraphicsFormat.None;
                desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
                desc.useMipMap = false;
                desc.autoGenerateMips = false;

                // The glow runs at half resolution: a tap's bilinear footprint then
                // spans two full-res texels, so the 9-tap kernel stops leaving gaps
                // at wide radii, and the two blur RTs cost a quarter as much.
                // The shader spreads by radius texels OF ITS SOURCE, so the vertical
                // pass — which reads the half-res intermediate — needs half the
                // radius to cover the same distance on screen as the horizontal one,
                // which still reads the full-res mask.
                RenderTextureDescriptor blurDesc = desc;
                blurDesc.width = Mathf.Max(1, desc.width / 2);
                blurDesc.height = Mathf.Max(1, desc.height / 2);
                float halfResRadius = blurRadius * 0.5f;

                // Clamp addressing: the default Repeat wraps the glow around the
                // screen edges when the kernel samples past the border.
                TextureHandle mask = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_DCL_HighlightMask", false, FilterMode.Bilinear, TextureWrapMode.Clamp);
                TextureHandle blurTmp = UniversalRenderer.CreateRenderGraphTexture(renderGraph, blurDesc, "_DCL_HighlightBlurTmp", false, FilterMode.Bilinear, TextureWrapMode.Clamp);
                TextureHandle blur = UniversalRenderer.CreateRenderGraphTexture(renderGraph, blurDesc, "_DCL_HighlightBlur", false, FilterMode.Bilinear, TextureWrapMode.Clamp);

                // ---- Pass 1: draw premultiplied silhouette into the mask RT ----
                using (var builder = renderGraph.AddUnsafePass<MaskPassData>("DCL.Highlight.Mask", out MaskPassData data, s_Sampler))
                {
                    data.Entries = scratch;
                    data.Count = count;
                    data.Material = maskMaterial;
                    data.Mask = mask;

                    builder.UseTexture(mask, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (MaskPassData d, UnsafeGraphContext ctx) =>
                    {
                        UnsafeCommandBuffer cmd = ctx.cmd;
                        cmd.SetRenderTarget(d.Mask);
                        cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);

                        for (int i = 0; i < d.Count; i++)
                        {
                            Renderer r = d.Entries[i].Renderer;
                            if (r == null || !r.enabled || r.forceRenderingOff || !r.gameObject.activeInHierarchy)
                                continue;

                            cmd.SetGlobalColor(s_HighlightColorId, d.Entries[i].Color);

                            for (int sub = 0; sub < d.Entries[i].Draws; sub++)
                                cmd.DrawRenderer(r, d.Material, sub, 0);
                        }
                    });
                }

                // ---- Pass 2: horizontal blur (full-res mask -> half-res blurTmp) ----
                AddBlurPass(renderGraph, "DCL.Highlight.BlurH", blurMaterial, 0, blurRadius, mask, blurTmp);

                // ---- Pass 3: vertical blur (blurTmp -> blur, both half-res) ----
                AddBlurPass(renderGraph, "DCL.Highlight.BlurV", blurMaterial, 1, halfResRadius, blurTmp, blur);

                // ---- Pass 4: additive rim composite onto the camera colour ----
                using (var builder = renderGraph.AddUnsafePass<CompositePassData>("DCL.Highlight.Composite", out CompositePassData data, s_Sampler))
                {
                    data.Material = compositeMaterial;
                    data.Mask = mask;
                    data.Blur = blur;
                    data.Target = cameraColor;

                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseTexture(blur, AccessFlags.Read);

                    // The composite blends against what is already in the camera
                    // colour, so the graph must know the pass reads it too.
                    builder.UseTexture(cameraColor, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc(static (CompositePassData d, UnsafeGraphContext ctx) =>
                    {
                        UnsafeCommandBuffer cmd = ctx.cmd;
                        cmd.SetRenderTarget(d.Target);
                        cmd.SetGlobalTexture(s_MaskTexId, d.Mask);
                        cmd.SetGlobalTexture(s_BlurTexId, d.Blur);
                        cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3, 1);
                    });
                }
            }

            // Also drains the bucket: an entry whose colour alpha has faded to zero
            // contributes nothing but keeps the feature enqueuing four passes every
            // frame, and the buckets are static, so it would never be released.
            private void GatherBucket(HighlightBucket bucket, ref int count, ref float maxIntensity)
            {
                deadKeys.Clear();

                foreach (KeyValuePair<Renderer, HighlightEntry> kv in bucket.Entries)
                {
                    if (kv.Key == null || kv.Value.Color.a <= 0f)
                    {
                        deadKeys.Add(kv.Key);
                        continue;
                    }

                    kv.Key.GetSharedMaterials(sharedMaterials);
                    int draws = Mathf.Clamp(sharedMaterials.Count, 1, Mathf.Max(1, kv.Value.SubMeshCount));

                    scratch[count].Renderer = kv.Key;
                    scratch[count].Color = kv.Value.Color;
                    scratch[count].Draws = draws;
                    if (kv.Value.Intensity > maxIntensity) maxIntensity = kv.Value.Intensity;
                    count++;
                }

                for (int i = 0; i < deadKeys.Count; i++)
                    bucket.Entries.Remove(deadKeys[i]);

                deadKeys.Clear();
            }

            private static void AddBlurPass(RenderGraph renderGraph, string passName, Material material,
                int shaderPass, float radius, TextureHandle source, TextureHandle target)
            {
                using var builder = renderGraph.AddUnsafePass<BlurPassData>(passName, out BlurPassData data, s_Sampler);

                data.Material = material;
                data.ShaderPass = shaderPass;
                data.Radius = radius;
                data.Source = source;
                data.Target = target;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(target, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (BlurPassData d, UnsafeGraphContext ctx) =>
                {
                    UnsafeCommandBuffer cmd = ctx.cmd;
                    cmd.SetRenderTarget(d.Target);
                    cmd.SetGlobalTexture(s_BlurSrcId, d.Source);
                    cmd.SetGlobalFloat(s_BlurRadiusId, d.Radius);
                    cmd.DrawProcedural(Matrix4x4.identity, d.Material, d.ShaderPass, MeshTopology.Triangles, 3, 1);
                });
            }
        }
    }
}
