using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Diagnostics.ReportsHandling;

namespace DCL.Rendering.Avatar
{
    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        class OutlineRenderPass : ScriptableRenderPass
        {
            // Debug
            private ReportData m_ReportData = new ReportData("DCL_RenderFeature_Outline_OutlinePass", ReportHint.SessionStatic);
            private const string profilerTag = "Custom Pass: Outline";

            private OutlineRendererFeature_Settings m_Settings;

            private Material outlineMaterial = null;
            private RTHandle outlineRTHandle = null;
            private RenderTextureDescriptor outlineRTDescriptor;

            private RTHandle depthNormalsRTHandle = null;

            private const string OUTLINE_TEXTURE_NAME = "_OutlineTexture";
            private static readonly int s_OutlineTextureID = Shader.PropertyToID(OUTLINE_TEXTURE_NAME);

            private const string COLOUR_TEXTURE_NAME = "_CameraColorTexture";
            private static readonly int s_ColourTextureID = Shader.PropertyToID(COLOUR_TEXTURE_NAME);
            private const string DEPTH_TEXTURE_NAME = "_CameraDepthTexture";
            private static readonly int s_DepthTextureID = Shader.PropertyToID(DEPTH_TEXTURE_NAME);
            private const string DEPTHNORMALS_TEXTURE_NAME = "_CameraDepthNormalsTexture";
            private static readonly int s_DepthNormalsTextureID = Shader.PropertyToID(DEPTHNORMALS_TEXTURE_NAME);

            private static readonly int s_OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
            private static readonly int s_DepthSensitivityID = Shader.PropertyToID("_DepthSensitivity");
            private static readonly int s_NormalsSensitivityID = Shader.PropertyToID("_NormalsSensitivity");
            private static readonly int s_ColorSensitivityID = Shader.PropertyToID("_ColorSensitivity");
            private static readonly int s_OutlineColorID = Shader.PropertyToID("_OutlineColor");

            //private const string k_ShaderName_Outline = "Avatar/Outline";
            //[SerializeField, HideInInspector] private Shader m_ShaderOutline = null;

            private enum ShaderPasses
            {
                OutlineRender = 0,
                OutlineDraw = 1
            }

            public void Setup(OutlineRendererFeature_Settings _Settings, Material _outlineMaterial, RTHandle _outlineRTHandle, RenderTextureDescriptor _outlineRTDescriptor,  RTHandle _depthNormalsRTHandle)
            {
                this.m_Settings = _Settings;
                this.outlineMaterial = _outlineMaterial;
                this.outlineRTHandle = _outlineRTHandle;
                this.outlineRTDescriptor = _outlineRTDescriptor;
                this.depthNormalsRTHandle = _depthNormalsRTHandle;
            }

            public OutlineRenderPass()
            {

            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in an performance manner.
            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                outlineMaterial.SetFloat(s_OutlineThicknessID, this.m_Settings.OutlineThickness);
                outlineMaterial.SetFloat(s_DepthSensitivityID, this.m_Settings.DepthSensitivity);
                outlineMaterial.SetFloat(s_NormalsSensitivityID, this.m_Settings.NormalsSensitivity);
                outlineMaterial.SetFloat(s_ColorSensitivityID, this.m_Settings.ColorSensitivity);
                outlineMaterial.SetVector(s_OutlineColorID, this.m_Settings.OutlineColor);
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("_OutlinePass");
                using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
                {
                    cmd.SetGlobalTexture(s_ColourTextureID, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    cmd.SetGlobalTexture(s_DepthTextureID, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                    cmd.SetGlobalTexture(s_DepthNormalsTextureID, depthNormalsRTHandle);
                    CoreUtils.SetRenderTarget(cmd, buffer: this.outlineRTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                    CoreUtils.DrawFullScreen(cmd, this.outlineMaterial, properties: null, (int)ShaderPasses.OutlineRender);

                    cmd.SetGlobalTexture(s_OutlineTextureID, this.outlineRTHandle);
                    CoreUtils.SetRenderTarget(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                    CoreUtils.DrawFullScreen(cmd, this.outlineMaterial, properties: null, (int)ShaderPasses.OutlineDraw);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            /// Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd)
            {

            }

            public void Dispose()
            {
                this.outlineRTHandle?.Release();
            }
        }
    }
}
