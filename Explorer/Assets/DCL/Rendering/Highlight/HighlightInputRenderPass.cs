﻿using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
// ReSharper disable InconsistentNaming

namespace DCL.Rendering.Highlight
{
    public partial class HighlightRendererFeature : ScriptableRendererFeature
    {
        private static readonly int highlightColour = Shader.PropertyToID("_HighlightColour");
        private static readonly int outlineWidth = Shader.PropertyToID("_Outline_Width");
        private static readonly int highlightObjectOffset = Shader.PropertyToID("_HighlightObjectOffset");

        public class HighlightInputRenderPass : ScriptableRenderPass
        {
            private enum ShaderPasses_Blur
            {
                HighlightInput_Blur_Horizontal = 0,
                HighlightInput_Blur_Vertical = 1
            }

            private const string PROFILER_TAG_ADDITIVE = "Custom Pass: Highlight Additive";
            private const string PROFILER_TAG_SUBTRACTIVE = "Custom Pass: Highlight Subtractive";
            private const string PROFILER_TAG_BLUR = "Custom Pass: Highlight Blur";
            private const string PROFILER_TAG_SECOND_COPY = "Custom Pass: Highlight Blur Second Copy";
            private const string PROFILER_TAG_FIRST_COPY = "Custom Pass: Highlight Blur First Copy";

            //private RTHandle destinationHandle;
            private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_InputPass", ReportHint.SessionStatic);

            private Material highLightInputMaterial;
            private Material highlightInputBlurMaterial;
            private RTHandle highLightRTHandle_Colour;
            private RTHandle highLightRTHandle_Depth;
            private RTHandle highLightRTHandle_Colour_Blur_Ping;
            private RTHandle highLightRTHandle_Colour_Blur_Pong;
            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;
            private RenderTextureDescriptor highLightRTDescriptor_Colour_Blur;

            private readonly IReadOnlyDictionary<Renderer, HighlightSettings> m_HighLightRenderers;

            private Dictionary<string, ProfilingSampler> m_ProfilingSamplers;

            private FilteringSettings m_FilteringSettings;

            public HighlightInputRenderPass(IReadOnlyDictionary<Renderer, HighlightSettings> highLightRenderers)
            {
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                m_HighLightRenderers = highLightRenderers;
                m_ProfilingSamplers = new Dictionary<string, ProfilingSampler>();
            }

            public void Setup(Material _highLightInputMaterial,
                Material _highlightInputBlurMaterial,
                RTHandle _highLightRTHandle_Colour,
                RenderTextureDescriptor _highLightRTDescriptor_Colour,
                RTHandle _highLightRTHandle_Depth,
                RenderTextureDescriptor _highLightRTDescriptor_Depth,
                RTHandle _highLightRTHandle_Colour_Blur_Ping,
                RTHandle _highLightRTHandle_Colour_Blur_Pong,
                RenderTextureDescriptor _highLightRTDescriptor_Colour_Blur)
            {
                highLightInputMaterial = _highLightInputMaterial;
                highlightInputBlurMaterial = _highlightInputBlurMaterial;
                highLightRTHandle_Colour = _highLightRTHandle_Colour;
                highLightRTDescriptor_Colour = _highLightRTDescriptor_Colour;
                highLightRTHandle_Depth = _highLightRTHandle_Depth;
                highLightRTDescriptor_Depth = _highLightRTDescriptor_Depth;
                highLightRTHandle_Colour_Blur_Ping = _highLightRTHandle_Colour_Blur_Ping;
                highLightRTHandle_Colour_Blur_Pong = _highLightRTHandle_Colour_Blur_Pong;
                highLightRTDescriptor_Colour_Blur = _highLightRTDescriptor_Colour_Blur;
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
                if (m_HighLightRenderers is not { Count: > 0 })
                    return;

                ExecuteCommand(context, renderingData, false, "_HighlightInputPass_Additive", GetSampler(PROFILER_TAG_ADDITIVE));

                ExecuteCommandCopy(context, renderingData, highLightRTHandle_Colour, highLightRTHandle_Colour_Blur_Ping, "_copyBuffer0", GetSampler(PROFILER_TAG_FIRST_COPY));
                uint nBlurCount = 4;
                int nBlurRT = ExecuteCommandBlur(context, renderingData, nBlurCount, "_HighlightInputPass_Blur", GetSampler(PROFILER_TAG_BLUR));

                ExecuteCommandCopy(context, renderingData, (nBlurRT % 2) > 0 ? highLightRTHandle_Colour_Blur_Ping : highLightRTHandle_Colour_Blur_Pong, highLightRTHandle_Colour, "_copyBuffer1", GetSampler(PROFILER_TAG_SECOND_COPY));

                ExecuteCommand(context, renderingData, true, "_HighlightInputPass_Subtractive", GetSampler(PROFILER_TAG_SUBTRACTIVE));
            }




            private void ExecuteCommand(ScriptableRenderContext context, RenderingData renderingData, bool clear, string bufferName, ProfilingSampler sampler)
            {
                CommandBuffer commandBuffer = CommandBufferPool.Get();

                using (new ProfilingScope(commandBuffer, sampler))
                {
                    foreach ((Renderer renderer, HighlightSettings settings) in m_HighLightRenderers)
                    {
                        if (renderer == null)
                            continue;

                        if (!renderer.enabled || renderer.forceRenderingOff)
                            continue;

                        GameObject gameObject = renderer.gameObject;

                        // Ignore disabled or culled by camera avatars
                        if (!gameObject.activeSelf || (renderingData.cameraData.camera.cullingMask & (1 << gameObject.layer)) == 0)
                            continue;

                        // We use a GPU Skinning based material
                        if (renderer.sharedMaterial == null)
                            continue;

                        int originalMaterialOutlinerPass = renderer.sharedMaterial.FindPass("Highlight");

                        if (originalMaterialOutlinerPass != -1)
                        {
                            //The material has a built in pass we can use
                            var materialToUse = new Material(renderer.sharedMaterial);
                            materialToUse.SetColor(highlightColour, !clear ? settings.Color : Color.clear);
                            materialToUse.SetFloat(outlineWidth, !clear ? settings.Width : 0);
                            materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                            commandBuffer.DrawRenderer(renderer, materialToUse, 0, originalMaterialOutlinerPass);
                        }
                        else
                        {
                            var materialToUse = new Material(highLightInputMaterial);
                            materialToUse.SetColor(highlightColour, !clear ? settings.Color : Color.clear);
                            materialToUse.SetFloat(outlineWidth, !clear ? settings.Width : 0);
                            materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                            commandBuffer.DrawRenderer(renderer, materialToUse, 0, 0);
                        }
                    }

                    context.ExecuteCommandBuffer(commandBuffer);
                    CommandBufferPool.Release(commandBuffer);
                }
            }

            private int ExecuteCommandBlur(ScriptableRenderContext context, RenderingData renderingData, uint _nBlurCount, string bufferName, ProfilingSampler profilingSampler)
            {
                int nOutputTexture = -1;
                if (_nBlurCount == 0)
                    return nOutputTexture;

                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    for (int nBlurPass = 0; nBlurPass < _nBlurCount; ++nBlurPass)
                    {
                        ++nOutputTexture;
                        cmd.SetGlobalTexture("_HighlightTexture", (nBlurPass % 2) < 1 ? highLightRTHandle_Colour_Blur_Ping : highLightRTHandle_Colour_Blur_Pong);
                        cmd.SetRenderTarget((nBlurPass % 2) > 0 ? highLightRTHandle_Colour_Blur_Ping : highLightRTHandle_Colour_Blur_Pong, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                        CoreUtils.DrawFullScreen(cmd, highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Horizontal);
                        CoreUtils.DrawFullScreen(cmd, highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Vertical);
                    }
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }

                return nOutputTexture;
            }

            private void ExecuteCommandCopy(ScriptableRenderContext context, RenderingData renderingData, RTHandle sourceRT, RTHandle destinationRT, string bufferName, ProfilingSampler profilingSampler)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blit(cmd, sourceRT, destinationRT);
                    context.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                }
            }

            private ProfilingSampler GetSampler(string bufferName)
            {
                ProfilingSampler sampler;
                if (m_ProfilingSamplers.TryGetValue(bufferName, out sampler)) return sampler;
                sampler = new ProfilingSampler(bufferName);
                m_ProfilingSamplers.Add(bufferName, sampler);
                return sampler;
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
