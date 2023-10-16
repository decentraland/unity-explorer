using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Diagnostics.ReportsHandling;

namespace DCL.Rendering.Avatar
{
    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        public class DepthNormalsRenderPass : ScriptableRenderPass
        {
            private ReportData m_ReportData = new ReportData("DCL_RenderFeature_Outline_DepthNormalsPass", ReportHint.SessionStatic);
            private const string profilerTag = "_DepthNormalsPass";

            private Material depthNormalsMaterial = null;
            private RTHandle depthNormalsRTHandle_Colour = null;
            private RTHandle depthNormalsRTHandle_Depth = null;
            private RenderTextureDescriptor depthNormalsRTDescriptor_Colour;
            private RenderTextureDescriptor depthNormalsRTDescriptor_Depth;

            private FilteringSettings m_FilteringSettings;
            //private RTHandle destinationHandle;
            ShaderTagId m_ShaderTagId = new ShaderTagId("DepthNormals");

            public DepthNormalsRenderPass() : base()
            {
                this.m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            }

            public void Setup(Material _depthNormalsMaterial,
                                RTHandle _depthNormalsRTHandle_Colour,
                                RenderTextureDescriptor _depthNormalsRTDescriptor_Colour,
                                RTHandle _depthNormalsRTHandle_Depth,
                                RenderTextureDescriptor _depthNormalsRTDescriptor_Depth)
            {
                this.depthNormalsMaterial = _depthNormalsMaterial;
                this.depthNormalsRTHandle_Colour = _depthNormalsRTHandle_Colour;
                this.depthNormalsRTDescriptor_Colour = _depthNormalsRTDescriptor_Colour;
                this.depthNormalsRTHandle_Depth = _depthNormalsRTHandle_Depth;
                this.depthNormalsRTDescriptor_Depth = _depthNormalsRTDescriptor_Depth;
            }

            // Configure the pass by creating a temporary render texture and
            // readying it for rendering
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(depthNormalsRTHandle_Colour, depthNormalsRTHandle_Depth);
                ConfigureClear(ClearFlag.All, Color.black);
                ConfigureColorStoreAction(RenderBufferStoreAction.Resolve);
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("_DepthNormalsPass");
                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {
                    // Create the draw settings, which configures a new draw call to the GPU
                    DrawingSettings drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);

                    // We cant to render all objects using our material
                    uint outlineLayerMask = 0;
                    m_FilteringSettings.renderingLayerMask = 2;//((uint)1 << outlineLayerMask);
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
                    m_FilteringSettings.renderingLayerMask = 1;
                    drawSettings.overrideMaterial = depthNormalsMaterial;
                    context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {

            }

            public void Dispose()
            {
                this.depthNormalsRTHandle_Colour?.Release();
                this.depthNormalsRTHandle_Depth?.Release();
            }
        }
    }
}
