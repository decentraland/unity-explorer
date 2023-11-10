using Diagnostics.ReportsHandling;
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class DCL_RenderFeature_ProceduralSkyBox : ScriptableRendererFeature
{
    public class DCL_RenderPass_GenerateSkyBox : ScriptableRenderPass
    {
        private enum ShaderPasses
        {
            CubeMapFace_Right = 0,
            CubeMapFace_Left = 1,
            CubeMapFace_Up = 2,
            CubeMapFace_Down = 3,
            CubeMapFace_Front = 4,
            CubeMapFace_Back = 5,
        }

        private const string profilerSkyBoxTag = "Custom Pass: GenerateSkyBox";
        private const string profilerStarBoxTag = "Custom Pass: GenerateStarBox";

        // Constants
        private const string k_SkyBoxCubemapTextureName = "_SkyBox_Cubemap_Texture";

        private static readonly int s_ParamsID = Shader.PropertyToID("_CurrentCubeFace");
        private static readonly int s_SunPosID = Shader.PropertyToID("_SunPos");
        private static readonly int s_SunColID = Shader.PropertyToID("_SunColour");
        private static readonly int s_SkyTintID = Shader.PropertyToID("_SkyTint");
        private static readonly int s_GroundColorID = Shader.PropertyToID("_GroundColor");
        private static readonly int s_SunSizeID = Shader.PropertyToID("_SunSize");
        private static readonly int s_SunSizeConvergenceID = Shader.PropertyToID("_SunSizeConvergence");
        private static readonly int s_AtmosphereThicknessID = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int s_ExposureID = Shader.PropertyToID("_Exposure");

        private static readonly int s_StarArraySRA0ID = Shader.PropertyToID("_starArraySRA0");
        private static readonly int s_StarArraySDEC0ID = Shader.PropertyToID("_starArraySDEC0");

        private int nDimensions = 1024;
        //private int nArraySize = 6;
        private ComputeShader StarsComputeShader;
        private RTHandle CubemapTextureArray;
        private ComputeBuffer starBuffer;
        private Vector2[] starList;

        // Statics
        //private static readonly int s_SkyBoxCubemapTextureID = Shader.PropertyToID(k_SkyBoxCubemapTextureName);

        // Debug
        private readonly ReportData m_ReportData = new ("DCL_RenderPass_GenerateSkyBox", ReportHint.SessionStatic);
        private Material m_Material_SkyBox_Generate;
        private Material m_Material_StarBox_Generate;
        private ProceduralSkyBoxSettings_Generate m_Settings_Generate;
        private RTHandle m_SkyBoxCubeMap_RTHandle;
        private RTHandle m_StarBoxCubeMap_RTHandle;

        internal DCL_RenderPass_GenerateSkyBox()
        {
            TextAsset asset = Resources.Load("BSC5") as TextAsset;
            if (asset != null)
            {
                BSC5 starlist = BSC5.Parse(asset.bytes);

                starList = new Vector2[starlist.entries.Length];

                for (int i = 0; i < starlist.entries.Length; ++i)
                {
                    starList[i].x = Convert.ToSingle(starlist.entries[i].SRA0);
                    starList[i].y = Convert.ToSingle(starlist.entries[i].SDEC0);
                }
            }
        }

        internal void Setup(ProceduralSkyBoxSettings_Generate _featureSettings, Material _skyboxMaterial, Material _starboxMaterial, RTHandle _SkyBoxRTHandle, RTHandle _StarBoxRTHandle, ComputeShader _StarsComputeShader, RTHandle _CubemapTextureArray)
        {
            m_Material_SkyBox_Generate = _skyboxMaterial;
            m_Material_StarBox_Generate = _starboxMaterial;
            m_Settings_Generate = _featureSettings;
            m_SkyBoxCubeMap_RTHandle = _SkyBoxRTHandle;
            m_StarBoxCubeMap_RTHandle = _StarBoxRTHandle;
            StarsComputeShader = _StarsComputeShader;
            CubemapTextureArray = _CubemapTextureArray;

            starBuffer = new ComputeBuffer(9110, sizeof(float) * 2, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            starBuffer.SetData(starList);
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Vector4 lightPos, lightColor, lightAttenuation, lightSpotDir, lightOcclusionChannel;
            UniversalRenderPipeline.InitializeLightConstants_Common(renderingData.lightData.visibleLights, renderingData.lightData.mainLightIndex, out lightPos, out lightColor, out lightAttenuation, out lightSpotDir, out lightOcclusionChannel);
            m_Material_SkyBox_Generate.SetVector(s_SunPosID, lightPos);
            m_Material_SkyBox_Generate.SetVector(s_SunColID, m_Settings_Generate.SunColour);
            m_Material_SkyBox_Generate.SetVector(s_SkyTintID, m_Settings_Generate.SkyTint);
            m_Material_SkyBox_Generate.SetVector(s_GroundColorID, m_Settings_Generate.GroundColor);
            m_Material_SkyBox_Generate.SetFloat(s_SunSizeID, m_Settings_Generate.SunSize);
            m_Material_SkyBox_Generate.SetFloat(s_SunSizeConvergenceID, m_Settings_Generate.SunSizeConvergence);
            m_Material_SkyBox_Generate.SetFloat(s_AtmosphereThicknessID, m_Settings_Generate.AtmosphereThickness);
            m_Material_SkyBox_Generate.SetFloat(s_ExposureID, m_Settings_Generate.Exposure);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Configure targets and clear color
            ConfigureTarget(m_SkyBoxCubeMap_RTHandle);
            ConfigureTarget(m_StarBoxCubeMap_RTHandle);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_Material_SkyBox_Generate == null)
            {
                ReportHub.LogError(m_ReportData, $"{GetType().Name}.Execute(): Missing material. DCL_RenderPass_GenerateSkyBox pass will not execute. Check for missing reference in the renderer resources.");
                return;
            }

            if (m_Material_StarBox_Generate == null)
            {
                ReportHub.LogError(m_ReportData, $"{GetType().Name}.Execute(): Missing material. DCL_RenderPass_GenerateSkyBox pass will not execute. Check for missing reference in the renderer resources.");
                return;
            }

            CommandBuffer cmdStarBox = CommandBufferPool.Get();
            using (new ProfilingScope(cmdStarBox, new ProfilingSampler(profilerStarBoxTag)))
            {
                string kernelName = "CSMain";
                int kernelIndex = StarsComputeShader.FindKernel(kernelName);
                StarsComputeShader.GetKernelThreadGroupSizes(kernelIndex, out uint xGroupSize, out uint yGroupSize, out uint zGroupSize);
                cmdStarBox.SetComputeTextureParam(StarsComputeShader, kernelIndex, "o_cubeMap", CubemapTextureArray);
                cmdStarBox.SetComputeIntParam(StarsComputeShader, "i_dimensions", nDimensions);
                cmdStarBox.SetComputeBufferParam(StarsComputeShader, kernelIndex, "StarBuffer", starBuffer);
                cmdStarBox.DispatchCompute(StarsComputeShader, kernelIndex, 9110 / (int)xGroupSize, (int)yGroupSize, (int)zGroupSize);
            }
            context.ExecuteCommandBuffer(cmdStarBox);
            cmdStarBox.Clear();
            CommandBufferPool.Release(cmdStarBox);

            // CommandBuffer cmdStarBox = CommandBufferPool.Get();
            // using (new ProfilingScope(cmdStarBox, new ProfilingSampler(profilerStarBoxTag)))
            // {
            //     // Uncomment below line for testing only, unnecessary during release.
            //     // CoreUtils.ClearCubemap(cmd, this.m_SkyBoxCubeMap_RTHandle.rt , Color.blue, clearMips : false);
            //
            //     // Due to an issue on AMD GPUs the globalvector doesn't work as expected so instead we moved to
            //     // a shader variant system. If fixed or work around from Unity is created then
            //     // switch to this look up to reduce shader variants
            //     // https://support.unity.com/hc/requests/1621458
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveX, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Right);
            //
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeX, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Left);
            //
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveY, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Up);
            //
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(3.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeY, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Down);
            //
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(4.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveZ, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Front);
            //
            //     //cmd.SetGlobalVector(s_ParamsID, new Vector4(5.0f, 0.0f, 0.0f, 0.0f));
            //     CoreUtils.SetRenderTarget(cmdStarBox, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeZ, depthSlice: 0);
            //     CoreUtils.DrawFullScreen(cmdStarBox, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Back);
            // }
            // context.ExecuteCommandBuffer(cmdStarBox);
            // CommandBufferPool.Release(cmdStarBox);

            CommandBuffer cmdSkyBox = CommandBufferPool.Get();
            using (new ProfilingScope(cmdSkyBox, new ProfilingSampler(profilerSkyBoxTag)))
            {
                // Uncomment below line for testing only, unnecessary during release.
                // CoreUtils.ClearCubemap(cmd, this.m_SkyBoxCubeMap_RTHandle.rt , Color.blue, clearMips : false);

                // Due to an issue on AMD GPUs the globalvector doesn't work as expected so instead we moved to
                // a shader variant system. If fixed or work around from Unity is created then
                // switch to this look up to reduce shader variants
                // https://support.unity.com/hc/requests/1621458
                //cmd.SetGlobalVector(s_ParamsID, new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Right);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Left);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Up);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(3.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Down);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(4.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Front);

                //cmd.SetGlobalVector(s_ParamsID, new Vector4(5.0f, 0.0f, 0.0f, 0.0f));
                CoreUtils.SetRenderTarget(cmdSkyBox, buffer: m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Back);
            }
            context.ExecuteCommandBuffer(cmdSkyBox);
            CommandBufferPool.Release(cmdSkyBox);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void dispose()
        {
            m_SkyBoxCubeMap_RTHandle?.Release();
            m_StarBoxCubeMap_RTHandle?.Release();
            CubemapTextureArray?.Release();
        }
    }
}
