using DCL.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
// ReSharper disable InconsistentNaming

namespace DCL.Rendering.RenderGraph.RenderFeatures.ObjectHighlight
{
    public class RenderPass_RenderResult : ScriptableRenderPass
    {
        private const string PROFILER_TAG_OUTPUT = "Object Highlight Output";

        //private RTHandle destinationHandle;
        private readonly ShaderTagId m_ShaderTagId = new ("Highlight");
        private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_OutputPass", ReportHint.SessionStatic);


        public Material m_highlightOutputMaterial;

        private TextureHandle highLightRTHandle_Colour;
        private TextureHandle highLightRTHandle_Depth;
        private TextureHandle highLightRTHandle_Colour_Blur_Ping;
        private TextureHandle highLightRTHandle_Colour_Blur_Pong;
        private RenderTextureDescriptor highLightRTDescriptor_Colour;
        private RenderTextureDescriptor highLightRTDescriptor_Depth;
        private RenderTextureDescriptor highLightRTDescriptor_Colour_Blur;

        private Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers;

        private Dictionary<string, ProfilingSampler> m_ProfilingSamplers;

        private FilteringSettings m_FilteringSettings;

        public RenderPass_RenderResult(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers)
        {
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque);
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

            highLightRTDescriptor_Colour_Blur = new RenderTextureDescriptor(
                Screen.width,
                Screen.height,
                RenderTextureFormat.Default,
                depthBufferBits: 0);
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            int cullingMask = cameraData.camera.cullingMask;

            // The following line ensures that the render pass doesn't blit from the back buffer.
            if (resourceData.isActiveTargetBackBuffer)
                return;

            var screenColorHandle = resourceData.activeColorTexture;
            var screenDepthStencilHandle = resourceData.activeDepthTexture;

            // This check is to avoid an error from the material preview in the scene
            if (!screenColorHandle.IsValid() || !screenDepthStencilHandle.IsValid())
                return;

            highLightRTDescriptor_Colour.width = cameraData.cameraTargetDescriptor.width;
            highLightRTDescriptor_Colour.height = cameraData.cameraTargetDescriptor.height;
            highLightRTDescriptor_Colour.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

            highLightRTDescriptor_Depth.width = cameraData.cameraTargetDescriptor.width;
            highLightRTDescriptor_Depth.height = cameraData.cameraTargetDescriptor.height;
            highLightRTDescriptor_Depth.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

            highLightRTDescriptor_Colour_Blur.width = cameraData.cameraTargetDescriptor.width;
            highLightRTDescriptor_Colour_Blur.height = cameraData.cameraTargetDescriptor.height;
            highLightRTDescriptor_Colour_Blur.msaaSamples = cameraData.cameraTargetDescriptor.msaaSamples;

            // TextureHandle highLightRTHandle_Colour = UniversalRenderer.CreateRenderGraphTexture(
            //     renderGraph,
            //     highLightRTDescriptor_Colour,
            //     "_Highlight_ColourTexture",
            //     clear: false);
            // TextureHandle highLightRTHandle_Depth = UniversalRenderer.CreateRenderGraphTexture(
            //     renderGraph,
            //     highLightRTDescriptor_Depth,
            //     "_Highlight_ColourTexture",
            //     clear: false);
            // TextureHandle highLightRTHandle_Colour_Blur_Ping= UniversalRenderer.CreateRenderGraphTexture(
            //     renderGraph,
            //     highLightRTDescriptor_Colour_Blur,
            //     "_Highlight_ColourTexture_Blur_Ping",
            //     clear: false);
            // TextureHandle highLightRTHandle_Colour_Blur_Pong= UniversalRenderer.CreateRenderGraphTexture(
            //     renderGraph,
            //     highLightRTDescriptor_Colour_Blur,
            //     "_Highlight_ColourTexture_Blur_Pong",
            //     clear: false);

            // using (var builder = renderGraph.AddRasterRenderPass<OutputPassData>(PROFILER_TAG_OUTPUT, out var passData))
            // {
            //     passData.highlightOutputMaterial = m_highlightOutputMaterial;
            //     passData.Source = highLightRTHandle_Colour;
            //     builder.SetRenderAttachment(screenColorHandle, 0);
            //     builder.SetRenderAttachmentDepth(screenDepthStencilHandle, AccessFlags.ReadWrite);
            //
            //     builder.SetRenderFunc((OutputPassData data, RasterGraphContext context) =>
            //     {
            //         Blitter.BlitTexture(context.cmd, data.Source, Vector2.one, data.highlightOutputMaterial, 0);
            //     });
            // }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }

        class OutputPassData
        {
            internal Material highlightOutputMaterial;
            internal Texture Source;
        }
    }
}
