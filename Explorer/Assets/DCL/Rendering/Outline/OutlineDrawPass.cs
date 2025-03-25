using DCL.Diagnostics;
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

            private ProfilingSampler m_Sampler = new (profilerTag);

            private FilteringSettings m_FilteringSettings;
            private List<Renderer> m_OutlineRenderers;

            public OutlineDrawPass(List<Renderer> _OutlineRenderers)
            {
                m_OutlineRenderers = _OutlineRenderers;
            }


            // Configure the pass by creating a temporary render texture and
            // readying it for rendering
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {

            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_OutlineRenderers is not { Count: > 0 })
                    return;

                CommandBuffer cmd = CommandBufferPool.Get("_OutlineDrawPass");
                CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);

                using (new ProfilingScope(cmd, m_Sampler))
                {
                    foreach (Renderer renderer in m_OutlineRenderers)
                    {
                        if (renderer == null)
                            continue;

                        if (!renderer.enabled || renderer.forceRenderingOff)
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

            }
        }
    }
}
