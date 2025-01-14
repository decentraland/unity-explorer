using DCL.Diagnostics;
using NSubstitute.ClearExtensions;
using System.Collections.Generic;
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
            private ProfilingSampler m_Sampler = new (profilerTag);

            private FilteringSettings m_FilteringSettings;
            private List<Renderer> m_OutlineRenderers;

            public OutlineDrawPass(List<Renderer> _OutlineRenderers)
            {
                m_OutlineRenderers = _OutlineRenderers;
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
                if (outlineRTHandle_Colour != null && outlineRTHandle_Depth != null)
                    ConfigureTarget(outlineRTHandle_Colour, outlineRTHandle_Depth);

                // ConfigureTarget(outlineRTHandle_Colour, outlineRTHandle_Depth);
                // ConfigureClear(ClearFlag.All, Color.clear);
                // ConfigureColorStoreAction(RenderBufferStoreAction.Resolve);
                // ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_OutlineRenderers is not { Count: > 0 })
                    return;

                CommandBuffer cmd = CommandBufferPool.Get("_OutlineDrawPass");

                using (new ProfilingScope(cmd, m_Sampler))
                {
                    foreach (Renderer renderer in m_OutlineRenderers)
                    {
                        if (renderer == null)
                            continue;

                        if (!renderer.enabled || renderer.forceRenderingOff)
                            continue;

                        GameObject gameObject = renderer.gameObject;

                        // Ignore disabled or culled by camera avatars
                        if (!gameObject.activeSelf || (renderingData.cameraData.camera.cullingMask & (1 << gameObject.layer)) == 0)
                            continue;

                        if (renderer.sharedMaterial == null)
                            continue;

                        int originalMaterialOutlinerPass = renderer.sharedMaterial.FindPass("Outline");

                        if (originalMaterialOutlinerPass != -1)
                        {
                            cmd.DrawRenderer(renderer, renderer.sharedMaterial, 0, originalMaterialOutlinerPass);
                        }
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                m_OutlineRenderers.Clear();
            }

            public void Dispose()
            {
                outlineRTHandle_Colour?.Release();
                outlineRTHandle_Depth?.Release();
            }
        }
    }
}
