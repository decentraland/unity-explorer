using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using DCL.Diagnostics;
using System.Collections.Generic;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.ObjectHighlight
{
    public partial class RenderFeature_ObjectHighlight
    {
        class RenderPass_DrawObjects : ScriptableRenderPass
        {
            class RenderObjectsPassData
            {
                internal IReadOnlyDictionary<Renderer, ObjectHighlightSettings> highlightRenderers;
                internal Material highLightInputMaterial;
                internal int cullingMask;
                internal bool clear;
            }

            class BlurPassData
            {
                internal TextureHandle PingSource;
                internal TextureHandle PongSource;
                internal Material highlightInputBlurMaterial;
            }

            class OutputPassData
            {
                internal Material highlightOutputMaterial;
                internal TextureHandle Source;
            }

            private static readonly int highlightColour = Shader.PropertyToID("_HighlightColour");
            private static readonly int outlineWidth = Shader.PropertyToID("_Outline_Width");
            private static readonly int highlightObjectOffset = Shader.PropertyToID("_HighlightObjectOffset");

            private enum ShaderPasses_Blur
            {
                HighlightInput_Blur_Horizontal = 0,
                HighlightInput_Blur_Vertical = 1
            }

            private const string PROFILER_TAG_ADDITIVE = "Highlight Additive";
            private const string PROFILER_TAG_SUBTRACTIVE = "Highlight Subtractive";
            private const string PROFILER_TAG_BLUR = "Highlight Blur";
            private const string PROFILER_TAG_SECOND_COPY = "Highlight Blur Second Copy";
            private const string PROFILER_TAG_FIRST_COPY = "Highlight Blur First Copy";
            private const string PROFILER_TAG_OUTPUT = "Object Highlight Output";

            //private RTHandle destinationHandle;
            private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_InputPass", ReportHint.SessionStatic);

            public Material m_highLightInputMaterial;
            public Material m_highlightInputBlurMaterial;
            public Material m_highlightOutputMaterial;

            // private TextureHandle highLightRTHandle_Colour;
            // private TextureHandle highLightRTHandle_Depth;
            // private TextureHandle highLightRTHandle_Colour_Blur_PingPong;

            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;

            private Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers;

            private Dictionary<string, ProfilingSampler> m_ProfilingSamplers;

            //private FilteringSettings m_FilteringSettings;

            public RenderPass_DrawObjects(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers)
            {
                //m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
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

            private static void ExecuteDrawObjects(RenderObjectsPassData data, RasterGraphContext context)
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
                        materialToUse.SetColor(highlightColour, !data.clear ? settings.Color : Color.clear);
                        materialToUse.SetFloat(outlineWidth, !data.clear ? settings.Width : 0);
                        materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                        context.cmd.DrawRenderer(renderer, materialToUse, 0, originalMaterialOutlinerPass);
                    }
                    else
                    {
                        var materialToUse = new Material(data.highLightInputMaterial);
                        materialToUse.SetColor(highlightColour, !data.clear ? settings.Color : Color.clear);
                        materialToUse.SetFloat(outlineWidth, !data.clear ? settings.Width : 0);
                        materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                        context.cmd.DrawRenderer(renderer, materialToUse, 0, 0);
                    }
                }
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                int cullingMask = cameraData.camera.cullingMask;

                // The following line ensures that the render pass doesn't blit from the back buffer.
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle screenColorHandle = resourceData.activeColorTexture;
                TextureHandle screenDepthStencilHandle = resourceData.activeDepthTexture;

                // This check is to avoid an error from the material preview in the scene
                if (!screenColorHandle.IsValid() || !screenDepthStencilHandle.IsValid())
                    return;

                highLightRTDescriptor_Colour.width = cameraData.cameraTargetDescriptor.width;
                highLightRTDescriptor_Colour.height = cameraData.cameraTargetDescriptor.height;
                highLightRTDescriptor_Colour.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

                highLightRTDescriptor_Depth.width = cameraData.cameraTargetDescriptor.width;
                highLightRTDescriptor_Depth.height = cameraData.cameraTargetDescriptor.height;
                highLightRTDescriptor_Depth.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

                // TextureHandle highLightRTHandle_Colour = UniversalRenderer.CreateRenderGraphTexture(
                //     renderGraph,
                //     highLightRTDescriptor_Colour,
                //     "_Highlight_ColourTexture",
                //     clear: true);
                //
                // TextureHandle highLightRTHandle_Depth = UniversalRenderer.CreateRenderGraphTexture(
                //     renderGraph,
                //     highLightRTDescriptor_Depth,
                //     "_Highlight_ColourTexture",
                //     clear: true);

                TextureHandle highLightRTHandle_Colour_Blur_PingPong = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    highLightRTDescriptor_Colour,
                    "_Highlight_ColourTexture_Blur_PingPong",
                    clear: true);

                var texRefExist = frameData.Contains<TexRefData>();
                var texRef = frameData.GetOrCreate<TexRefData>();

                // First time running this pass. Fetch ref from active color buffer.
                if (!texRefExist)
                {
                    // For this first occurence we would like
                    texRef.textureColour = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        highLightRTDescriptor_Colour,
                        "_Highlight_ColourTexture",
                        clear: true);

                    texRef.textureDepth = UniversalRenderer.CreateRenderGraphTexture(
                        renderGraph,
                        highLightRTDescriptor_Depth,
                        "_Highlight_DepthTexture",
                        clear: true);
                }

                using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(PROFILER_TAG_ADDITIVE, out var passData))
                {


                    // Configure pass data
                    passData.highlightRenderers = m_HighLightRenderers;
                    passData.highLightInputMaterial = m_highLightInputMaterial;
                    passData.cullingMask = cullingMask;
                    passData.clear = false;

                    builder.SetRenderAttachment(texRef.textureColour, 0);
                    builder.SetRenderAttachmentDepth(texRef.textureDepth, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                        ExecuteDrawObjects(data, context));
                }

                using (var builder = renderGraph.AddUnsafePass<BlurPassData>(PROFILER_TAG_BLUR, out var passData))
                {
                    //var texRef = frameData.GetOrCreate<TexRefData>();

                    // Configure pass data
                    passData.highlightInputBlurMaterial = m_highlightInputBlurMaterial;
                    passData.PingSource = texRef.textureColour;
                    passData.PongSource = highLightRTHandle_Colour_Blur_PingPong;

                    builder.UseTexture(passData.PingSource);
                    builder.UseTexture(passData.PongSource);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((BlurPassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        uint _nBlurCount = 4;

                        using (new ProfilingScope(cmd, profilingSampler))
                        {
                            for (int nBlurPass = 0; nBlurPass < _nBlurCount; ++nBlurPass)
                            {
                                cmd.SetGlobalTexture("_HighlightTexture", (nBlurPass % 2) < 1 ? data.PingSource : data.PongSource);
                                cmd.SetRenderTarget((nBlurPass % 2) > 0 ? data.PingSource : data.PongSource, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                                CoreUtils.DrawFullScreen(cmd, data.highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Horizontal);
                                CoreUtils.DrawFullScreen(cmd, data.highlightInputBlurMaterial, properties: null, (int)ShaderPasses_Blur.HighlightInput_Blur_Vertical);
                            }
                        }
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<RenderObjectsPassData>(PROFILER_TAG_SUBTRACTIVE, out var passData))
                {
                    //var texRef = frameData.GetOrCreate<TexRefData>();

                    // Configure pass data
                    passData.highlightRenderers = m_HighLightRenderers;
                    passData.highLightInputMaterial = m_highLightInputMaterial;

                    builder.SetRenderAttachment(texRef.textureColour, 0);
                    builder.SetRenderAttachmentDepth(texRef.textureDepth, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((RenderObjectsPassData data, RasterGraphContext context) =>
                        ExecuteDrawObjects(data, context));
                }

                using (var builder = renderGraph.AddRasterRenderPass<OutputPassData>(PROFILER_TAG_OUTPUT, out var passData))
                {
                    //var texRef = frameData.GetOrCreate<TexRefData>();

                    passData.highlightOutputMaterial = m_highlightOutputMaterial;
                    passData.Source = texRef.textureColour;
                    builder.UseTexture(passData.Source);
                    builder.SetRenderAttachment(screenColorHandle, 0);
                    builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((OutputPassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.Source, Vector2.one, data.highlightOutputMaterial, 0);
                    });
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {

            }
        }
    }
}
