using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline
{
    /// <summary>
    ///     Draws the flagged avatar renderers with the inverted-hull outline material,
    ///     straight to the active camera colour + depth. Runs before the opaque queue
    ///     so the subsequent opaque avatar draw cuts out the interior and leaves the
    ///     rim. No mask RT, no fullscreen composite — Vulkan-safe.
    /// </summary>
    public class RenderPass_OutlineDraw : ScriptableRenderPass
    {
        private const string PASS_NAME = "DCL.AvatarOutline";
        private static readonly ProfilingSampler s_Sampler = new (PASS_NAME);

        // Avatar materials carry the renderer's first vertex in the global
        // compute-skinned buffer as these two integers; the outline shader reads the
        // skinned vertex at their sum + vertex id when this per-draw global is >= 0.
        private static readonly int s_SkinnedBaseId = Shader.PropertyToID("_DCL_OutlineSkinnedBase");
        private static readonly int s_LastAvatarVertCountId = Shader.PropertyToID("_lastAvatarVertCount");
        private static readonly int s_LastWearableVertCountId = Shader.PropertyToID("_lastWearableVertCount");

        private readonly List<Renderer> outlineRenderers;
        private readonly Material outlineMaterial;

        private sealed class PassData
        {
            internal Renderer[] Renderers;
            internal int[] Draws;
            internal float[] SkinnedBases;
            internal int Count;
            internal Material Material;
        }

        // Reused so the per-frame snapshot does not allocate.
        private Renderer[] scratch = new Renderer[256];
        private int[] scratchDraws = new int[256];
        private float[] scratchBases = new float[256];
        private readonly List<Material> sharedMaterials = new (8);

        public RenderPass_OutlineDraw(List<Renderer> renderers, Material material)
        {
            outlineRenderers = renderers;
            outlineMaterial = material;
            profilingSampler = s_Sampler;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // The producing system only appends to the bucket, so this pass owns
            // draining it, and it drains before any early exit. OnCameraCleanup is
            // not part of the RenderGraph path.
            int count = Snapshot();
            outlineRenderers.Clear();

            if (outlineMaterial == null || count == 0)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // Don't attempt to draw straight into the back buffer (no readable depth).
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle color = resourceData.activeColorTexture;
            TextureHandle depth = resourceData.activeDepthTexture;
            if (!color.IsValid() || !depth.IsValid())
                return;

            using var builder = renderGraph.AddRasterRenderPass<PassData>(PASS_NAME, out PassData passData, s_Sampler);

            passData.Renderers = scratch;
            passData.Draws = scratchDraws;
            passData.SkinnedBases = scratchBases;
            passData.Count = count;
            passData.Material = outlineMaterial;

            // The outline covers only the extruded rim, so the colour target has to
            // keep what the earlier passes put there.
            builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
            builder.SetRenderAttachmentDepth(depth, AccessFlags.ReadWrite);
            builder.AllowPassCulling(false);
            // The skinned vertex base is a global set between draws.
            builder.AllowGlobalStateModification(true);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                RasterCommandBuffer cmd = context.cmd;
                for (int i = 0; i < data.Count; i++)
                {
                    Renderer r = data.Renderers[i];
                    if (r == null || !r.enabled || r.forceRenderingOff)
                        continue;

                    cmd.SetGlobalFloat(s_SkinnedBaseId, data.SkinnedBases[i]);

                    for (int sub = 0; sub < data.Draws[i]; sub++)
                        cmd.DrawRenderer(r, data.Material, sub, 0);
                }
            });
        }

        // Copies the bucket into the reusable arrays and resolves each renderer's
        // draw count: sharedMaterials would allocate a fresh array per renderer per
        // frame, and a submesh index past the mesh's range makes DrawRenderer error.
        private int Snapshot()
        {
            int count = outlineRenderers.Count;

            if (scratch.Length < count)
            {
                int capacity = Mathf.NextPowerOfTwo(count);
                scratch = new Renderer[capacity];
                scratchDraws = new int[capacity];
                scratchBases = new float[capacity];
            }

            int written = 0;
            for (int i = 0; i < count; i++)
            {
                Renderer r = outlineRenderers[i];
                if (r == null) continue;

                r.GetSharedMaterials(sharedMaterials);
                scratch[written] = r;
                scratchDraws[written] = Mathf.Clamp(sharedMaterials.Count, 1, Mathf.Max(1, SubMeshCount(r)));
                scratchBases[written] = SkinnedVertexBase(sharedMaterials.Count > 0 ? sharedMaterials[0] : null);
                written++;
            }

            return written;
        }

        // Avatar renderers keep their bind-pose mesh and are posed by the compute
        // skinning pass, so the outline has to read the posed vertex from the global
        // buffer: the material's two vertex-count integers locate it. Negative means
        // the renderer's own mesh attributes are the posed geometry.
        internal static float SkinnedVertexBase(Material material)
        {
            if (material == null || !material.HasInteger(s_LastAvatarVertCountId) || !material.HasInteger(s_LastWearableVertCountId))
                return -1f;

            return material.GetInteger(s_LastAvatarVertCountId) + material.GetInteger(s_LastWearableVertCountId);
        }

        private static int SubMeshCount(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
                return skinned.sharedMesh != null ? skinned.sharedMesh.subMeshCount : 1;

            if (renderer.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                return filter.sharedMesh.subMeshCount;

            return 1;
        }
    }
}
