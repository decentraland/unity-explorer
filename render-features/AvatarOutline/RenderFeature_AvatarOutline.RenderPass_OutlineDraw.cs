using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.AvatarOutline
{
    public class RenderPass_OutlineDraw : ScriptableRenderPass
    {
        private const string DrawOutlineObjectsPassName = "DrawOutlineObjectsPass";
        private List<Renderer> m_OutlineRenderers;

        private class RenderObjectsPassData
        {
            internal Renderer[] outlineRenderers;
            internal int originalMaterialOutlinerPass;
        }

        public RenderPass_OutlineDraw(List<Renderer> _OutlineRenderers)
        {
            m_OutlineRenderers = _OutlineRenderers;
        }

        private static void ExecuteDrawOutlineObjects(RenderObjectsPassData data, RasterGraphContext context)
        {
            // Render all the outlined objects to the temp texture
            foreach (Renderer objectRenderer in data.outlineRenderers)
            {
                if (objectRenderer == null)
                    continue;

                if (!objectRenderer.enabled || objectRenderer.forceRenderingOff)
                    continue;

                if (objectRenderer.sharedMaterial == null)
                    continue;
                
                context.cmd.DrawRenderer(objectRenderer, objectRenderer.sharedMaterial, 0, data.originalMaterialOutlinerPass);
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_OutlineRenderers.Count <= 0)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

            // The following line ensures that the render pass doesn't blit from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle screenColorHandle = resourceData.activeColorTexture;
            TextureHandle screenDepthStencilHandle = resourceData.activeDepthTexture;

            // This check is to avoid an error from the material preview in the scene
            if (!screenColorHandle.IsValid() || !screenDepthStencilHandle.IsValid())
                return;

            // Draw objects-to-outline pass
            using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(DrawOutlineObjectsPassName, out var passData))
            {
                int originalMaterialOutlinerPass = m_OutlineRenderers[0].sharedMaterial.FindPass("Outline");

                // Configure pass data
                passData.outlineRenderers = m_OutlineRenderers.ToArray();
                passData.originalMaterialOutlinerPass = originalMaterialOutlinerPass;

                builder.SetRenderAttachment(screenColorHandle, 0);

                // Make sure we also read from the active stencil buffer,
                // which was written to in the Draw objects-to-outline pass
                // and is used here to cut out the inside of the outline.
                builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.ReadWrite);

                builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                    ExecuteDrawOutlineObjects(data, context));
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            m_OutlineRenderers.Clear();
        }
    }
}
