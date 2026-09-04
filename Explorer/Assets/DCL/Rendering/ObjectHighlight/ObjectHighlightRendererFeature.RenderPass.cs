using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.ObjectHighlight
{
    public partial class ObjectHighlightRendererFeature
    {
        private class DrawObjectsPass : ScriptableRenderPass
        {
            private const int BLUR_ITERATIONS = 4;
            private const string AVATAR_SHADER_NAME = "DCL/DCL_Toon";
            private const string AVATAR_HIGHLIGHT_PASS_NAME = "Highlight";

            private static readonly ProfilerMarker AVATAR_HIGHLIGHT_MARKER = new ("AvatarHighlight_DrawObjects");

            // Declared as plain uniforms by DCL_ToonHighlight.hlsl in unity-shared-dependencies. Nothing
            // else writes them and no assembly reference ties the two together, so a rename there breaks
            // the avatar highlight silently.
            private static readonly int AVATAR_COLOR = Shader.PropertyToID("_Highlight_Colour");
            private static readonly int AVATAR_WIDTH = Shader.PropertyToID("_Highlight_Width");
            private static readonly int AVATAR_OBJECT_OFFSET = Shader.PropertyToID("_Highlight_ObjectOffset");
            private static readonly int AVATAR_NEAREST_DISTANCE = Shader.PropertyToID("_Highlight_Nearest_Distance");
            private static readonly int AVATAR_FARTHEST_DISTANCE = Shader.PropertyToID("_Highlight_Farthest_Distance");
            private static readonly int AVATAR_Z_OVER_DRAW_MODE = Shader.PropertyToID("_Highlight_ZOverDrawMode");
            private static readonly int AVATAR_OFFSET_Z = Shader.PropertyToID("_Highlight_Offset_Z");

            // ObjectHighlightInput.shader. Globals rather than material properties so one shared material
            // serves every renderer; a per-draw Material copy would leak for as long as anything is hovered.
            private static readonly int OBJECT_OFFSET = Shader.PropertyToID("_Highlight_ObjectOffset");
            private static readonly int COLOR = Shader.PropertyToID("_Highlight_Color");
            private static readonly int OUTLINE_WIDTH = Shader.PropertyToID("_Highlight_OutlineWidth");
            private static readonly int OUTLINE_DEPTH_BIAS = Shader.PropertyToID("_Highlight_OutlineDepthBias");
            private static readonly int SURFACE_DEPTH_BIAS = Shader.PropertyToID("_Highlight_SurfaceDepthBias");
            private static readonly int FILL = Shader.PropertyToID("_Highlight_Fill");
            private static readonly int RIM = Shader.PropertyToID("_Highlight_Rim");
            private static readonly int FRESNEL_POWER = Shader.PropertyToID("_Highlight_FresnelPower");
            private static readonly int MAX_OPACITY = Shader.PropertyToID("_Highlight_MaxOpacity");
            private static readonly int PULSE = Shader.PropertyToID("_Highlight_Pulse");
            private static readonly int HIGHLIGHT_TEXTURE = Shader.PropertyToID("_HighlightTexture");

            private enum InputPass
            {
                Outline = 0,
                Surface = 1,
            }

            private enum BlurPass
            {
                Horizontal = 0,
                Vertical = 1,
            }

            private readonly Dictionary<Renderer, ObjectHighlightSettings> highlightRenderers;
            private readonly Dictionary<Renderer, ObjectHighlightSettings> avatarHighlightRenderers;
            private readonly Material inputMaterial;
            private readonly Material blurMaterial;
            private readonly Material outputMaterial;

            /// <summary>
            ///     Null when DCL_Toon is missing from the build, which leaves avatars unhighlighted rather
            ///     than throwing while building the render graph.
            /// </summary>
            private readonly Material? avatarMaterial;
            private readonly int avatarPassID;

            private RenderTextureDescriptor colourDescriptor;

            public DrawObjectsPass(
                Dictionary<Renderer, ObjectHighlightSettings> highlightRenderers,
                Dictionary<Renderer, ObjectHighlightSettings> avatarHighlightRenderers,
                Material inputMaterial,
                Material blurMaterial,
                Material outputMaterial)
            {
                this.highlightRenderers = highlightRenderers;
                this.avatarHighlightRenderers = avatarHighlightRenderers;
                this.inputMaterial = inputMaterial;
                this.blurMaterial = blurMaterial;
                this.outputMaterial = outputMaterial;

                colourDescriptor = new RenderTextureDescriptor(
                    Screen.width,
                    Screen.height,
                    RenderTextureFormat.Default,
                    depthBufferBits: 0);

                Shader avatarShader = Shader.Find(AVATAR_SHADER_NAME);

                if (avatarShader == null)
                    return;

                avatarMaterial = new Material(avatarShader);
                avatarPassID = avatarMaterial.FindPass(AVATAR_HIGHLIGHT_PASS_NAME);
                avatarMaterial.SetVector(AVATAR_OBJECT_OFFSET, Vector3.zero);
                avatarMaterial.SetFloat(AVATAR_NEAREST_DISTANCE, 0.5f);
                avatarMaterial.SetFloat(AVATAR_FARTHEST_DISTANCE, 100.0f);
                avatarMaterial.SetFloat(AVATAR_Z_OVER_DRAW_MODE, 0.0f);
                avatarMaterial.SetFloat(AVATAR_OFFSET_Z, 0.0f);
                avatarMaterial.EnableKeyword("_DCL_COMPUTE_SKINNING");
            }

            public void Dispose()
            {
                CoreUtils.Destroy(avatarMaterial);
            }

            private static bool IsDrawable(Renderer renderer, int cullingMask)
            {
                if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                    return false;

                GameObject gameObject = renderer.gameObject;

                // Ignore objects that are disabled or culled by the camera
                return gameObject.activeSelf && (cullingMask & (1 << gameObject.layer)) != 0;
            }

            private static void DrawObjects(CommandBuffer cmd, PassData data, InputPass shaderPass)
            {
                foreach ((Renderer renderer, ObjectHighlightSettings settings) in data.highlightRenderers)
                {
                    if (!IsDrawable(renderer, data.cullingMask))
                        continue;

                    cmd.SetGlobalVector(OBJECT_OFFSET, Vector3.zero);
                    cmd.SetGlobalColor(COLOR, settings.Color);
                    cmd.SetGlobalFloat(OUTLINE_WIDTH, settings.Width);
                    cmd.SetGlobalFloat(OUTLINE_DEPTH_BIAS, settings.OutlineDepthBias);
                    cmd.SetGlobalFloat(SURFACE_DEPTH_BIAS, settings.SurfaceDepthBias);
                    cmd.SetGlobalFloat(FILL, settings.Fill);
                    cmd.SetGlobalFloat(RIM, settings.Rim);
                    cmd.SetGlobalFloat(FRESNEL_POWER, settings.FresnelPower);
                    cmd.SetGlobalFloat(MAX_OPACITY, settings.MaxOpacity);
                    cmd.SetGlobalFloat(PULSE, settings.Pulse);
                    cmd.DrawRenderer(renderer, data.inputMaterial, 0, (int)shaderPass);
                }
            }

            private static void DrawAvatarObjects(CommandBuffer cmd, PassData data, bool clear)
            {
                if (data.avatarPassID < 0)
                    return;

                AVATAR_HIGHLIGHT_MARKER.Begin();

                foreach ((Renderer renderer, ObjectHighlightSettings settings) in data.avatarHighlightRenderers)
                {
                    if (!IsDrawable(renderer, data.cullingMask))
                        continue;

                    // Avatars are drawn through their own GPU-skinned material, not the override
                    if (renderer.sharedMaterial == null || renderer.sharedMaterial.renderQueue != (int)RenderQueue.Geometry)
                        continue;

                    cmd.SetGlobalColor(AVATAR_COLOR, !clear ? settings.Color : Color.clear);
                    cmd.SetGlobalFloat(AVATAR_WIDTH, !clear ? settings.Width : 0.0f);
                    cmd.DrawRenderer(renderer, renderer.sharedMaterial, 0, data.avatarPassID);
                }

                AVATAR_HIGHLIGHT_MARKER.End();
            }

            private static void DrawBlur(CommandBuffer cmd, Material material, BlurPass blurPass)
            {
                CoreUtils.DrawFullScreen(cmd, material, properties: null, (int)blurPass);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (highlightRenderers.Count <= 0 && avatarHighlightRenderers.Count <= 0)
                    return;

                using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("FullHighlight", out PassData passData);

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

                // Keeps the pass from blitting out of the back buffer
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                // Avoids an error from the material preview in the scene view
                if (!resourceData.activeColorTexture.IsValid() || !resourceData.activeDepthTexture.IsValid())
                    return;

                colourDescriptor.width = cameraData.cameraTargetDescriptor.width;
                colourDescriptor.height = cameraData.cameraTargetDescriptor.height;
                colourDescriptor.msaaSamples = 1;

                passData.highlightRenderers = highlightRenderers;
                passData.avatarHighlightRenderers = avatarHighlightRenderers;
                passData.inputMaterial = inputMaterial;
                passData.blurMaterial = blurMaterial;
                passData.outputMaterial = outputMaterial;
                passData.avatarPassID = avatarMaterial != null ? avatarPassID : -1;
                passData.ping = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, colourDescriptor, "_Highlight_ColourTexture", clear: true);
                passData.pong = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, colourDescriptor, "_Highlight_ColourTexture_Blur_PingPong", clear: true);
                passData.backBufferColour = resourceData.activeColorTexture;
                passData.backBufferDepth = resourceData.activeDepthTexture;
                passData.cullingMask = cameraData.camera.cullingMask;

                builder.UseTexture(passData.ping, AccessFlags.ReadWrite);
                builder.UseTexture(passData.pong, AccessFlags.ReadWrite);
                builder.UseTexture(passData.backBufferColour, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Silhouette, fattened outwards
                    context.cmd.SetRenderTarget(data.ping);
                    DrawObjects(cmd, data, InputPass.Outline);
                    DrawAvatarObjects(cmd, data, clear: false);

                    for (var blurPass = 0; blurPass < BLUR_ITERATIONS; ++blurPass)
                    {
                        cmd.SetGlobalTexture(HIGHLIGHT_TEXTURE, blurPass % 2 < 1 ? data.ping : data.pong);
                        context.cmd.SetRenderTarget(blurPass % 2 > 0 ? data.ping : data.pong);
                        DrawBlur(cmd, data.blurMaterial, BlurPass.Horizontal);
                        DrawBlur(cmd, data.blurMaterial, BlurPass.Vertical);
                    }

                    // Replace the silhouette's interior. This both erases the blur's inward bleed and shades
                    // the surface. Avatars only get the erase: they carry their own highlight pass in
                    // DCL_Toon, which has no surface treatment.
                    context.cmd.SetRenderTarget(data.ping);
                    DrawObjects(cmd, data, InputPass.Surface);
                    DrawAvatarObjects(cmd, data, clear: true);

                    cmd.SetGlobalTexture(HIGHLIGHT_TEXTURE, data.ping);
                    context.cmd.SetRenderTarget(data.backBufferColour, data.backBufferDepth);
                    CoreUtils.DrawFullScreen(cmd, data.outputMaterial);
                });
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                highlightRenderers.Clear();
                avatarHighlightRenderers.Clear();
            }

            private class PassData
            {
                internal IReadOnlyDictionary<Renderer, ObjectHighlightSettings> highlightRenderers = null!;
                internal IReadOnlyDictionary<Renderer, ObjectHighlightSettings> avatarHighlightRenderers = null!;
                internal Material inputMaterial = null!;
                internal Material blurMaterial = null!;
                internal Material outputMaterial = null!;
                internal int avatarPassID;
                internal TextureHandle ping;
                internal TextureHandle pong;
                internal TextureHandle backBufferColour;
                internal TextureHandle backBufferDepth;
                internal int cullingMask;
            }
        }
    }
}
