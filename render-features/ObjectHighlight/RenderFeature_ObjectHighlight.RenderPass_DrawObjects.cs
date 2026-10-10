using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;
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
                internal IReadOnlyDictionary<Renderer, ObjectHighlightSettings> highlightRenderers_Avatars;
                internal Material highLightInputMaterial;
                internal Material highlightInputBlurMaterial;
                internal Material highlightOutputMaterial;
                internal TextureHandle PingSource;
                internal TextureHandle PongSource;
                internal TextureHandle BackBufferColourSource;
                internal TextureHandle BackBufferDepthSource;
                internal int cullingMask;
            }

            static readonly ProfilerMarker avatarHighlightMarker = new ProfilerMarker("AvatarHighlight_DrawObjects");

            // Avatar Highlight Shader Variables
            private static readonly int Highlight_lastAvatarVertCount = Shader.PropertyToID("_lastAvatarVertCount");
            private static readonly int Highlight_lastWearableVertCount = Shader.PropertyToID("_lastWearableVertCount");
            private static readonly int Highlight_lastAvatarVertCount2 = Shader.PropertyToID("_lastAvatarVertCount2");
            private static readonly int Highlight_lastWearableVertCount2 = Shader.PropertyToID("_lastWearableVertCount2");
            private static readonly int Highlight_Colour = Shader.PropertyToID("_Highlight_Colour");
            private static readonly int Highlight_ObjectOffset = Shader.PropertyToID("_Highlight_ObjectOffset");
            private static readonly int Highlight_Width = Shader.PropertyToID("_Highlight_Width");
            private static readonly int Highlight_Nearest_Distance = Shader.PropertyToID("_Highlight_Nearest_Distance");
            private static readonly int Highlight_Farthest_Distance = Shader.PropertyToID("_Highlight_Farthest_Distance");
            private static readonly int Highlight_ZOverDrawMode = Shader.PropertyToID("_Highlight_ZOverDrawMode");
            private static readonly int Highlight_Offset_Z = Shader.PropertyToID("_Highlight_Offset_Z");

            // Generic Highlight Shader Variables
            private static readonly int highlightColour = Shader.PropertyToID("_HighlightColour");
            private static readonly int outlineWidth = Shader.PropertyToID("_Outline_Width");
            private static readonly int highlightObjectOffset = Shader.PropertyToID("_HighlightObjectOffset");
            private const string HIGHLIGHT_TEXTURE_NAME = "_HighlightTexture";
            private static readonly int s_HighlightTextureID = Shader.PropertyToID(HIGHLIGHT_TEXTURE_NAME);

            private static Material avatarHighlight;
            private static int nHighlightPassID;

            private enum ShaderPasses_Blur
            {
                HighlightInput_Blur_Horizontal = 0,
                HighlightInput_Blur_Vertical = 1
            }

            private enum ShaderPasses
            {
                HighlightOutput = 0,
            }

            public Material m_highLightInputMaterial;
            public Material m_highlightInputBlurMaterial;
            public Material m_highlightOutputMaterial;

            private RenderTextureDescriptor highLightRTDescriptor_Colour;
            private RenderTextureDescriptor highLightRTDescriptor_Depth;

            private Dictionary<Renderer, ObjectHighlightSettings> m_HighLightRenderers;
            private Dictionary<Renderer, ObjectHighlightSettings> m_highlightRenderers_Avatars;

            private Dictionary<string, ProfilingSampler> m_ProfilingSamplers;

            public RenderPass_DrawObjects(Dictionary<Renderer, ObjectHighlightSettings> highLightRenderers,
                Dictionary<Renderer, ObjectHighlightSettings> highlightRenderers_Avatars)
            {
                m_HighLightRenderers = highLightRenderers;
                m_highlightRenderers_Avatars = highlightRenderers_Avatars;
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

                avatarHighlight = new Material(Shader.Find("DCL/DCL_Toon"));
                nHighlightPassID = avatarHighlight.FindPass("Highlight");
                avatarHighlight.SetVector(Highlight_ObjectOffset, Vector3.zero);
                avatarHighlight.SetFloat(Highlight_Nearest_Distance, 0.5f);
                avatarHighlight.SetFloat(Highlight_Farthest_Distance, 100.0f);
                avatarHighlight.SetFloat(Highlight_ZOverDrawMode, 0.0f);
                avatarHighlight.SetFloat(Highlight_Offset_Z, 0.0f);
                avatarHighlight.EnableKeyword("_DCL_COMPUTE_SKINNING");
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

                    var materialToUse = new Material(data.highLightInputMaterial);
                    materialToUse.SetColor(highlightColour, !bClear ? settings.Color : Color.clear);
                    materialToUse.SetFloat(outlineWidth, !bClear ? settings.Width : 0);
                    materialToUse.SetVector(highlightObjectOffset, Vector3.zero);
                    cmd.DrawRenderer(renderer, materialToUse, 0, 0);
                }
            }

            private static void DrawObjects_Avatar(CommandBuffer cmd, FullHighlightPassData data, bool bClear)
            {
                avatarHighlightMarker.Begin();
                foreach ((Renderer renderer, ObjectHighlightSettings settings) in data.highlightRenderers_Avatars)
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

					if (renderer.sharedMaterial.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Geometry)
                        continue;

                    cmd.SetGlobalColor(Highlight_Colour, !bClear ? settings.Color : Color.clear);
                    cmd.SetGlobalFloat(Highlight_Width, !bClear ? settings.Width : 0.0f);
                    //cmd.SetGlobalInteger(Highlight_lastAvatarVertCount2, renderer.sharedMaterial.GetInteger(Highlight_lastAvatarVertCount));
                    //cmd.SetGlobalInteger(Highlight_lastWearableVertCount2, renderer.sharedMaterial.GetInteger(Highlight_lastWearableVertCount));
                    cmd.DrawRenderer(renderer, renderer.sharedMaterial, 0, nHighlightPassID);
                }
                avatarHighlightMarker.End();
            }
            
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (m_HighLightRenderers.Count <= 0 && m_highlightRenderers_Avatars.Count <= 0)
                    return;

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
                    passData.highlightRenderers_Avatars = m_highlightRenderers_Avatars;
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
                    builder.UseTexture(passData.BackBufferColourSource, AccessFlags.Write);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc((FullHighlightPassData data, UnsafeGraphContext context) =>
                    {
                        CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                        context.cmd.SetRenderTarget(data.PingSource);
                        DrawObjects(cmd, data, false);
                        DrawObjects_Avatar(cmd, data, false);

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
                        DrawObjects_Avatar(cmd, data, true);

                        cmd.SetGlobalTexture(s_HighlightTextureID, data.PingSource);
                        context.cmd.SetRenderTarget(data.BackBufferColourSource, data.BackBufferDepthSource);
                        CoreUtils.DrawFullScreen(cmd, data.highlightOutputMaterial, properties: null, (int)ShaderPasses.HighlightOutput);
                    });
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                m_HighLightRenderers.Clear();
                m_highlightRenderers_Avatars.Clear();
            }
        }
    }
}
