using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Highlight
{
    public partial class HighlightRendererFeature : ScriptableRendererFeature
    {
        public class HighlightInputRenderPass : ScriptableRenderPass
        {
            private const string profilerTagInput = "Custom Pass: Highlight Additive";
            private const string profilerTagOutput = "Custom Pass: Highlight Subtractive";

            //private RTHandle destinationHandle;
            private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_InputPass", ReportHint.SessionStatic);

            private Material highLightInputMaterial;
            private RTHandle highLightRTHandle_Colour;
            private RTHandle highLightRTHandle_Depth;
            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;

            private readonly List<Renderer> m_HighLightRenderers;

            private FilteringSettings m_FilteringSettings;

            public HighlightInputRenderPass(List<Renderer> _HighLightRenderers)
            {
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                m_HighLightRenderers = _HighLightRenderers;
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
                ConfigureClear(ClearFlag.All, Color.clear);
                ConfigureColorStoreAction(RenderBufferStoreAction.Resolve);
                ConfigureDepthStoreAction(RenderBufferStoreAction.DontCare);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_HighLightRenderers != null && m_HighLightRenderers.Count > 0)
                {
                    CommandBuffer cmd_additive = CommandBufferPool.Get("_HighlightInputPass_Additive");
                    using (new ProfilingScope(cmd_additive, new ProfilingSampler(profilerTagInput)))
                    {
                        Material materialToUse = new Material(Shader.Find("DCL/DCL_Toon"));

                        foreach (var objectrenderer in m_HighLightRenderers)
                        {
                            if (objectrenderer == null)
                                continue;

                            // Ignore disabled or culled by camera avatars
                            if (!objectrenderer.gameObject.activeSelf || (renderingData.cameraData.camera.cullingMask & (1 << objectrenderer.gameObject.layer)) == 0)
                                continue;

                            // We use a GPU Skinning based material
                            if (objectrenderer.material != null)
                            {
                                int originalMaterialOutlinerPass = objectrenderer.material.FindPass("Highlight");
                                if (originalMaterialOutlinerPass != -1)
                                {
                                    //The material has a built in pass we can use
                                    materialToUse = new Material(objectrenderer.material);
                                    materialToUse.SetColor("_HighlightColour", Color.red);
                                    materialToUse.SetFloat("_Outline_Width", 1.0f);
                                    Vector4 vOriginOffset = new Vector4(-(objectrenderer.bounds.extents.x * 0.5f), -(objectrenderer.bounds.extents.y * 0.5f), -(objectrenderer.bounds.extents.z * 0.5f), 0.0f);
                                    materialToUse.SetVector("_HighlightObjectOffset", vOriginOffset);
                                    cmd_additive.DrawRenderer(objectrenderer, materialToUse, 0, originalMaterialOutlinerPass);
                                }
                                else
                                {
                                    materialToUse = highLightInputMaterial;
                                    materialToUse.SetColor("_HighlightColour", Color.red);
                                    materialToUse.SetFloat("_Outline_Width", 1.0f);
                                    Vector4 vOriginOffset = new Vector4(-(objectrenderer.bounds.extents.x * 0.5f), -(objectrenderer.bounds.extents.y * 0.5f), -(objectrenderer.bounds.extents.z * 0.5f), 0.0f);
                                    materialToUse.SetVector("_HighlightObjectOffset", vOriginOffset);
                                    cmd_additive.DrawRenderer(objectrenderer, materialToUse, 0, 0);
                                }
                            }
                        }

                        context.ExecuteCommandBuffer(cmd_additive);
                        CommandBufferPool.Release(cmd_additive);
                    }

                    CommandBuffer cmd_subtractive = CommandBufferPool.Get("_HighlightInputPass_Subtractive");
                    using (new ProfilingScope(cmd_subtractive, new ProfilingSampler(profilerTagOutput)))
                    {
                        Material materialToUse = new Material(Shader.Find("DCL/DCL_Toon"));

                        foreach (var objectrenderer in m_HighLightRenderers)
                        {
                            if (objectrenderer == null)
                                continue;

                            // Ignore disabled or culled by camera avatars
                            if (!objectrenderer.gameObject.activeSelf || (renderingData.cameraData.camera.cullingMask & (1 << objectrenderer.gameObject.layer)) == 0)
                                continue;

                            // We use a GPU Skinning based material
                            if (objectrenderer.material != null)
                            {
                                int originalMaterialOutlinerPass = objectrenderer.material.FindPass("Highlight");

                                if (originalMaterialOutlinerPass != -1)
                                {
                                    //The material has a built in pass we can use
                                    materialToUse = new Material(objectrenderer.material);
                                    materialToUse.SetColor("_HighlightColour", Color.clear);
                                    materialToUse.SetFloat("_Outline_Width", 0.0f);
                                    Vector4 vOriginOffset = new Vector4(-(objectrenderer.bounds.extents.x * 0.5f), -(objectrenderer.bounds.extents.y * 0.5f), -(objectrenderer.bounds.extents.z * 0.5f), 0.0f);
                                    materialToUse.SetVector("_HighlightObjectOffset", vOriginOffset);
                                    cmd_subtractive.DrawRenderer(objectrenderer, materialToUse, 0, originalMaterialOutlinerPass);
                                }
                                else
                                {
                                    //The material has a built in pass we can use
                                    materialToUse = highLightInputMaterial;
                                    materialToUse.SetColor("_HighlightColour", Color.clear);
                                    materialToUse.SetFloat("_Outline_Width", 0.0f);
                                    Vector4 vOriginOffset = new Vector4(-(objectrenderer.bounds.extents.x * 0.5f), -(objectrenderer.bounds.extents.y * 0.5f), -(objectrenderer.bounds.extents.z * 0.5f), 0.0f);
                                    materialToUse.SetVector("_HighlightObjectOffset", vOriginOffset);
                                    cmd_subtractive.DrawRenderer(objectrenderer, materialToUse, 0, 0);
                                }
                            }
                        }

                        context.ExecuteCommandBuffer(cmd_subtractive);
                        CommandBufferPool.Release(cmd_subtractive);
                    }
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
