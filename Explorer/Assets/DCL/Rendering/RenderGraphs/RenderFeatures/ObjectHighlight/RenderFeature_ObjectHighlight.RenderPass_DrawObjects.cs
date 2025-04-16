using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using DCL.Diagnostics;
using System.Collections.Generic;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public partial class RenderFeature_ObjectHighlight
    {
        class RenderPass_DrawObjects : ScriptableRenderPass
        {
            class FullHighlightPassData
            {
                internal IReadOnlyDictionary<Renderer, ObjectHighlightSettings> highlightRenderers;
                internal Material highLightInputMaterial;
                internal Material highlightInputBlurMaterial;
                internal Material highlightOutputMaterial;
                internal TextureHandle PingSource;
                internal TextureHandle PongSource;
                internal TextureHandle BackBufferColourSource;
                internal TextureHandle BackBufferDepthSource;
                internal int cullingMask;
            }

            private static readonly int highlightColour = Shader.PropertyToID("_HighlightColour");
            private static readonly int outlineWidth = Shader.PropertyToID("_Outline_Width");
            private static readonly int highlightObjectOffset = Shader.PropertyToID("_HighlightObjectOffset");
            private const string HIGHLIGHT_TEXTURE_NAME = "_HighlightTexture";
            private static readonly int s_HighlightTextureID = Shader.PropertyToID(HIGHLIGHT_TEXTURE_NAME);

            private enum ShaderPasses_Blur
            {
                HighlightInput_Blur_Horizontal = 0,
                HighlightInput_Blur_Vertical = 1
            }

            private enum ShaderPasses
            {
                HighlightOutput = 0,
            }

            private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_InputPass", ReportHint.SessionStatic);

            public Material m_highLightInputMaterial;
            public Material m_highlightInputBlurMaterial;
            public Material m_highlightOutputMaterial;

            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;

            private Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers;

            private Dictionary<string, ProfilingSampler> m_ProfilingSamplers;

            public RenderPass_DrawObjects(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers)
            {
                m_HighLightRenderers = highLightRenderers;
                m_ProfilingSamplers = new Dictionary<string, ProfilingSampler>();

                highLightRTDescriptor_Colour = new RenderTextureDescriptor(
                    Screen.width,
                    Screen.height,
                    RenderTextureFormat.Default,
                    depthBufferBits: 0);

                highLightRTDescriptor_Depth = new RenderTextureDescriptor(
                    Screen.width,
                    Screen.height,
                    RenderTextureFormat.Default,
                    depthBufferBits: 32);
            }

            private static void DrawObjects(CommandBuffer cmd, FullHighlightPassData data, bool bClear)
            {
                foreach ((Renderer renderer, ObjectHighlightSettings settings) in data.highlightRenderers)
                {
                    if (renderer == null)
                        continue;

                    if (!renderer.enabled || renderer.forceRenderingOff)
                        continue;

                    GameObject gameObject = renderer.gameObject;

                    // Ignore disabled or culled by camera avatars
                    if (!gameObject.activeSelf || (data.cullingMask & (1 << gameObject.layer)) == 0)
                        continue;

                    // We use a GPU Skinning based material
                    if (renderer.sharedMaterial == null)
                        continue;

                    int originalMaterialOutlinerPass = renderer.sharedMaterial.FindPass("Highlight");

                    if (originalMaterialOutlinerPass != -1)
                    {
                        //The material has a built in pass we can use
                        var materialToUse = new Material(renderer.sharedMaterial);
                        materialToUse.SetColor(highlightColour, !bClear ? settings.Color : Color.clear);
                        materialToUse.SetFloat(outlineWidth, !bClear ? settings.Width : 0);
                        materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                        cmd.DrawRenderer(renderer, materialToUse, 0, originalMaterialOutlinerPass);
                    }
                    else
                    {
                        var materialToUse = new Material(data.highLightInputMaterial);
                        materialToUse.SetColor(highlightColour, !bClear ? settings.Color : Color.clear);
                        materialToUse.SetFloat(outlineWidth, !bClear ? settings.Width : 0);
                        materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                        cmd.DrawRenderer(renderer, materialToUse, 0, 0);
                    }
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddUnsafePass<FullHighlightPassData>("FullHighlight", out var passData))
                {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                    // The following line ensures that the render pass doesn't blit from the back buffer.
                    if (resourceData.isActiveTargetBackBuffer)
                        return;

                    // This check is to avoid an error from the material preview in the scene
                    if (!resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid())
                        return;

                    highLightRTDescriptor_Colour.width = cameraData.cameraTargetDescriptor.width;
                    highLightRTDescriptor_Colour.height = cameraData.cameraTargetDescriptor.height;
                    highLightRTDescriptor_Colour.msaaSamples = 1;

                    highLightRTDescriptor_Depth.width = cameraData.cameraTargetDescriptor.width;
                    highLightRTDescriptor_Depth.height = cameraData.cameraTargetDescriptor.height;
                    highLightRTDescriptor_Depth.msaaSamples = 1;

                    passData.highlightRenderers = m_HighLightRenderers;
                    passData.highLightInputMaterial = m_highLightInputMaterial;
                    passData.highlightInputBlurMaterial = m_highlightInputBlurMaterial;
                    passData.highlightOutputMaterial = m_highlightOutputMaterial;
                    passData.PingSource = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        highLightRTDescriptor_Colour,
                        "_Highlight_ColourTexture",
                        clear: true);
                    passData.PongSource = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        highLightRTDescriptor_Colour,
                        "_Highlight_ColourTexture_Blur_PingPong",
                        clear: true);
                    passData.BackBufferColourSource = resourceData.activeColorTexture;
                    passData.BackBufferDepthSource = resourceData.activeDepthTexture;
                    passData.cullingMask = cameraData.camera.cullingMask;

                    builder.UseTexture(passData.PingSource, AccessFlags.ReadWrite);
                    builder.UseTexture(passData.PongSource, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((FullHighlightPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        context.cmd.SetRenderTarget(data.PingSource);
                        DrawObjects(cmd, data, false);

                        uint _nBlurCount = 4;
                        for (int nBlurPass = 0; nBlurPass < _nBlurCount; ++nBlurPass)
                        {
                            cmd.SetGlobalTexture(s_HighlightTextureID, (nBlurPass % 2) < 1 ? data.PingSource : data.PongSource);
                            context.cmd.SetRenderTarget((nBlurPass % 2) > 0 ? data.PingSource : data.PongSource);
                            CoreUtils.DrawFullScreen(cmd, data.highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Horizontal);
                            CoreUtils.DrawFullScreen(cmd, data.highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Vertical);
                        }

                        context.cmd.SetRenderTarget(data.PingSource);
                        DrawObjects(cmd, data, true);

                        cmd.SetGlobalTexture(s_HighlightTextureID, data.PingSource);
                        context.cmd.SetRenderTarget(data.BackBufferColourSource, data.BackBufferDepthSource);
                        CoreUtils.DrawFullScreen(cmd, data.highlightOutputMaterial, properties: null, (int)ShaderPasses.HighlightOutput);
                    });
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {

            }
        }
    }
}
