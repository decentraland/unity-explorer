using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Avatar
{
    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        public class OutlineDrawPass : ScriptableRenderPass
        {
            private const string profilerTag = "_OutlineDrawPass";
            private readonly ShaderTagId m_ShaderTagId = new ("Outline");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Outline_OutlineDrawPass", ReportHint.SessionStatic);

            private RTHandle outlineRTHandle_Colour;
            private RTHandle outlineRTHandle_Depth;
            private RenderTextureDescriptor outlineRTDescriptor_Colour;
            private RenderTextureDescriptor outlineRTDescriptor_Depth;

            private FilteringSettings m_FilteringSettings;

            public OutlineDrawPass()
            {
            }

            public void Setup(  RTHandle _outlineRTHandle_Colour,
                                RTHandle _outlineRTHandle_Depth,
                                RenderTextureDescriptor _outlineRTDescriptor_Colour,
                                RenderTextureDescriptor _outlineRTDescriptor_Depth)
            {
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                outlineRTHandle_Colour = _outlineRTHandle_Colour;
                outlineRTHandle_Depth = _outlineRTHandle_Depth;
                outlineRTDescriptor_Colour = _outlineRTDescriptor_Colour;
                outlineRTDescriptor_Depth = _outlineRTDescriptor_Depth;
            }

            // Configure the pass by creating a temporary render texture and
            // readying it for rendering
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(outlineRTHandle_Colour, outlineRTHandle_Depth);
                // ConfigureTarget(outlineRTHandle_Colour, outlineRTHandle_Depth);
                // ConfigureClear(ClearFlag.All, Color.clear);
                // ConfigureColorStoreAction(RenderBufferStoreAction.Resolve);
                // ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("_OutlineDrawPass");

                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {
                    // Create the draw settings, which configures a new draw call to the GPU
                    //CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                    DrawingSettings drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
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
                outlineRTHandle_Colour?.Release();
                outlineRTHandle_Depth?.Release();
            }
        }
    }
}
