using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Diagnostics.ReportsHandling;

public partial class DCL_RenderFeature_ProceduralSkyBox : ScriptableRendererFeature
{
    public class DCL_RenderPass_GenerateSkyBox : ScriptableRenderPass
    {
        // Debug
        private ReportData m_ReportData = new ReportData("DCL_RenderPass_GenerateSkyBox", ReportHint.SessionStatic);

        private const string profilerTag = "Custom Pass: GenerateSkyBox";
        private Material m_Material_Generate;
        private ProceduralSkyBoxSettings_Generate m_Settings_Generate;
        private RTHandle m_SkyBoxCubeMap_RTHandle;

        private static readonly int s_ParamsID = Shader.PropertyToID("_CurrentCubeFace");
        private static readonly int s_SunPosID = Shader.PropertyToID("_SunPos");
        private static readonly int s_SunColID = Shader.PropertyToID("_SunColour");

        // Constants
        private const string k_SkyBoxCubemapTextureName = "_SkyBox_Cubemap_Texture";

        // Statics
        private static readonly int s_SkyBoxCubemapTextureID = Shader.PropertyToID(k_SkyBoxCubemapTextureName);
        
        private enum ShaderPasses
        {
            CubeMapFace_Right = 0,
            CubeMapFace_Left = 1,
            CubeMapFace_Up = 2,
            CubeMapFace_Down = 3,
            CubeMapFace_Front = 4,
            CubeMapFace_Back = 5
        }

        internal DCL_RenderPass_GenerateSkyBox()
        {

        }

        internal void Setup(ProceduralSkyBoxSettings_Generate _featureSettings, Material _material, RTHandle _RTHandle)
        {
            this.m_Material_Generate = _material;
            this.m_Settings_Generate = _featureSettings;
            this.m_SkyBoxCubeMap_RTHandle = _RTHandle;
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            m_Material_Generate.SetVector(s_SunPosID, this.m_Settings_Generate.SunPos);
            m_Material_Generate.SetVector(s_SunColID, this.m_Settings_Generate.SunColour);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Configure targets and clear color
            ConfigureTarget(this.m_SkyBoxCubeMap_RTHandle);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (this.m_Material_Generate == null)
            {
                ReportHub.LogError(this.m_ReportData, $"{GetType().Name}.Execute(): Missing material. DCL_RenderPass_GenerateSkyBox pass will not execute. Check for missing reference in the renderer resources.");
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
            {
                // Uncomment below line for testing only, unnecessary during release.
                // CoreUtils.ClearCubemap(cmd, this.m_SkyBoxCubeMap_RTHandle.rt , Color.blue, clearMips : false);

                // Due to an issue on AMD GPUs the globalvector doesn't work as expected so instead we moved to
                // a shader variant system. If fixed or work around from Unity is created then
                // switch to this look up to reduce shader variants
                // https://support.unity.com/hc/requests/1621458
                //cmd.SetGlobalVector(s_ParamsID, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Right);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Left);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Up);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(3.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Down);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(4.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Front);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(5.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Back);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }

        public void dispose()
        {
            this.m_SkyBoxCubeMap_RTHandle?.Release();
        }

    }
}