using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Highlight
{
    public partial class HighlightRendererFeature : ScriptableRendererFeature
    {
        private class HighlightOutputRenderPass : ScriptableRenderPass
        {
            private enum ShaderPasses
            {
                HighlightOutput = 0,
            }

            private const string profilerTag = "Custom Pass: Highlight Output";

            // Texture IDs for Outline Shader - defined in Outline.HLSL
            private const string HIGHLIGHT_TEXTURE_NAME = "_HighlightTexture";
            private const string COLOUR_TEXTURE_NAME = "_CameraColorTexture";
            private const string DEPTH_TEXTURE_NAME = "_CameraDepthTexture";
            //private const string DEPTHNORMALS_TEXTURE_NAME = "_CameraDepthNormalsTexture";

            private static readonly int s_OutlineTextureID = Shader.PropertyToID(HIGHLIGHT_TEXTURE_NAME);
            private static readonly int s_ColourTextureID = Shader.PropertyToID(COLOUR_TEXTURE_NAME);
            private static readonly int s_DepthTextureID = Shader.PropertyToID(DEPTH_TEXTURE_NAME);
            //private static readonly int s_DepthNormalsTextureID = Shader.PropertyToID(DEPTHNORMALS_TEXTURE_NAME);

            // Feature Settings - Shader values
            private static readonly int s_OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
            private static readonly int s_DepthSensitivityID = Shader.PropertyToID("_DepthSensitivity");
            private static readonly int s_NormalsSensitivityID = Shader.PropertyToID("_NormalsSensitivity");
            private static readonly int s_ColorSensitivityID = Shader.PropertyToID("_ColorSensitivity");
            private static readonly int s_OutlineColorID = Shader.PropertyToID("_OutlineColor");

            // Debug
            private ReportData m_ReportData = new ("DCL_RenderFeature_Highlight_OutputPass", ReportHint.SessionStatic);

            private HighlightRendererFeature_Settings m_Settings;

            private Material highlightOutputMaterial;
            private RTHandle highlightRTHandle;
            private RenderTextureDescriptor highlightRTDescriptor;
            //private RTHandle depthNormalsRTHandle;

            public void Setup(HighlightRendererFeature_Settings _Settings, Material _highlightOutputMaterial, RTHandle _highlightRTHandle, RenderTextureDescriptor _highlightRTDescriptor)
            {
                m_Settings = _Settings;
                highlightOutputMaterial = _highlightOutputMaterial;
                highlightRTHandle = _highlightRTHandle;
                highlightRTDescriptor = _highlightRTDescriptor;
                //depthNormalsRTHandle = _depthNormalsRTHandle;
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in an performance manner.
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                highlightOutputMaterial.SetFloat(s_OutlineThicknessID, m_Settings.OutlineThickness);
                highlightOutputMaterial.SetFloat(s_DepthSensitivityID, m_Settings.DepthSensitivity);
                highlightOutputMaterial.SetFloat(s_NormalsSensitivityID, m_Settings.NormalsSensitivity);
                highlightOutputMaterial.SetFloat(s_ColorSensitivityID, m_Settings.ColorSensitivity);
                highlightOutputMaterial.SetVector(s_OutlineColorID, m_Settings.OutlineColor);
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext _context, ref RenderingData _renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("_HighlightOutputPass");

                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {
                    cmd.SetGlobalTexture(s_OutlineTextureID, highlightRTHandle);
                    cmd.SetGlobalTexture(s_ColourTextureID, _renderingData.cameraData.renderer.cameraColorTargetHandle);
                    cmd.SetGlobalTexture(s_DepthTextureID, _renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    CoreUtils.SetRenderTarget(cmd, _renderingData.cameraData.renderer.cameraColorTargetHandle, _renderingData.cameraData.renderer.cameraDepthTargetHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                    CoreUtils.DrawFullScreen(cmd, highlightOutputMaterial, properties: null, (int)ShaderPasses.HighlightOutput);
                }

                _context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd) { }

            public void Dispose()
            {
                highlightRTHandle?.Release();
            }
        }
    }
}
