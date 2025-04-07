using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using DCL.Diagnostics;
using System.Collections.Generic;

namespace DCL.Rendering.RenderGraph.RenderFeatures.AvatarOutline
{
    public class RenderPass_OutlineDraw : ScriptableRenderPass
    {
        private const string profilerTag = "_OutlineDrawPass";
        private readonly ShaderTagId m_ShaderTagId = new ("Outline");
        private ReportData m_ReportData = new ("DCL_RenderFeature_Outline_OutlineDrawPass", ReportHint.SessionStatic);
        private const string DrawOutlineObjectsPassName = "DrawOutlineObjectsPass";

        private ProfilingSampler m_Sampler = new (profilerTag);

        private FilteringSettings m_FilteringSettings;
        private List<Renderer> m_OutlineRenderers;
        public RenderPass_OutlineDraw(List<Renderer> _OutlineRenderers)
        {
            m_OutlineRenderers = _OutlineRenderers;
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            // The following line ensures that the render pass doesn't blit
            // from the back buffer.
            // if (resourceData.isActiveTargetBackBuffer)
            //     return;

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            var screenColorHandle = resourceData.activeColorTexture;
            var screenDepthStencilHandle = resourceData.activeDepthTexture;

            // This check is to avoid an error from the material preview in the scene
            if (!screenColorHandle.IsValid() || !screenDepthStencilHandle.IsValid())
                return;

            // Draw objects-to-outline pass
            using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(DrawOutlineObjectsPassName, out var passData))
            {
                // Configure pass data
                passData.outlineRenderers = m_OutlineRenderers.ToArray();

                builder.SetRenderAttachment(screenColorHandle, 0);

                // Make sure we also read from the active stencil buffer,
                // which was written to in the Draw objects-to-outline pass
                // and is used here to cut out the inside of the outline.
                builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.ReadWrite);

                builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                    ExecuteDrawOutlineObjects(data, context));
            }
        }

        private static void ExecuteDrawOutlineObjects(RenderObjectsPassData data, RasterGraphContext context)
        {
            //int originalMaterialOutlinerPass = objectRenderer.sharedMaterial.FindPass("Outline");
            // Render all the outlined objects to the temp texture
            foreach (Renderer objectRenderer in data.outlineRenderers)
            {
                // Skip null renderers
                //if (objectRenderer)
                {
                    // if (!objectRenderer.enabled || objectRenderer.forceRenderingOff)
                    //     continue;
                    //
                    // if (objectRenderer.sharedMaterial == null)
                    //     continue;


                    //if (originalMaterialOutlinerPass != -1)
                    {
                        context.cmd.DrawRenderer(objectRenderer, objectRenderer.sharedMaterial, 0, 0);
                    }
                }
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            m_OutlineRenderers.Clear();
        }

        class RenderObjectsPassData
        {
            internal Renderer[] outlineRenderers;
        }
    }
}
