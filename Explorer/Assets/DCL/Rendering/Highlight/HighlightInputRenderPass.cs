using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Highlight
{
    public partial class HighlightRendererFeature : ScriptableRendererFeature
    {
        public class HighlightInputRenderPass : ScriptableRenderPass
        {
            private const string profilerTag = "Custom Pass: Highlight Input";

            //private RTHandle destinationHandle;
            private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_InputPass", ReportHint.SessionStatic);

            private Material highLightInputMaterial;
            private RTHandle highLightRTHandle_Colour;
            private RTHandle highLightRTHandle_Depth;
            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;

            private FilteringSettings m_FilteringSettings;

            public HighlightInputRenderPass()
            {
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            }

            public void Setup(Material _highLightInputMaterial,
                                RTHandle _highLightRTHandle_Colour,
                                RenderTextureDescriptor _highLightRTDescriptor_Colour,
                                RTHandle _highLightRTHandle_Depth,
                                RenderTextureDescriptor _highLightRTDescriptor_Depth)
            {
                highLightInputMaterial = _highLightInputMaterial;
                highLightRTHandle_Colour = _highLightRTHandle_Colour;
                highLightRTDescriptor_Colour = _highLightRTDescriptor_Colour;
                highLightRTHandle_Depth = _highLightRTHandle_Depth;
                highLightRTDescriptor_Depth = _highLightRTDescriptor_Depth;
            }

            // Configure the pass by creating a temporary render texture and
            // readying it for rendering
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureTarget(highLightRTHandle_Colour, highLightRTHandle_Depth);
                ConfigureClear(ClearFlag.All, Color.black);
                ConfigureColorStoreAction(RenderBufferStoreAction.Resolve);
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                {
                    CommandBuffer cmd = CommandBufferPool.Get("_HighlightInputPass");
                    using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                    {
                        DrawingSettings drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }

                {
                    CommandBuffer cmd = CommandBufferPool.Get("_HighlightInputPass");
                    using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                    {
                        DrawingSettings drawSettings = CreateDrawingSettings(m_ShaderTagId, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                        context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            public override void FrameCleanup(CommandBuffer cmd) { }

            public void Dispose()
            {
                highLightRTHandle_Colour?.Release();
                highLightRTHandle_Depth?.Release();
            }
        }
    }
}

// private void DrawAvatar()
// {
//     if (m_HighLightRenderers != null && m_HighLightRenderers.empty)
//     {
//         CommandBuffer cmd = CommandBufferPool.Get("_HighlightInputPass");
//         using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
//         {
//             for(objectrenderer in m_HighLightRenderers)
//             {
//                 if (objectrenderer.renderer == null)
//                     continue;
//
//                 //Ignore disabled or culled by camera avatars
//                 if (!objectrenderer.renderer.gameObject.activeSelf || (renderingData.cameraData.camera.cullingMask & (1 << objectrenderer.renderer.gameObject.layer)) == 0)
//                     continue;
//
//                 for (var i = 0; i < objectrenderer.meshCount; ++i)
//                 {
//                     Material materialToUse = null;
//
//                     // We use a GPU Skinning based material
//                     if (avatar.renderer.materials[i] != null)
//                     {
//                         int originalMaterialOutlinerPass = avatar.renderer.materials[i].FindPass("Highlight");
//                         if (originalMaterialOutlinerPass != -1)
//                         {
//                             //The material has a built in pass we can use
//                             cmd.DrawRenderer(avatar.renderer, avatar.renderer.materials[i], i, originalMaterialOutlinerPass);
//                         }
//                         else
//                         {
//                             cmd.DrawRenderer(avatar.renderer, materialToUse, i, 0);
//                         }
//                     }
//                 }
//             }
//         }
//     }
// }
