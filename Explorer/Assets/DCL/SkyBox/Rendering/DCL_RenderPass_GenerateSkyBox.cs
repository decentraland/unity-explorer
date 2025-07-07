using DCL.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.SkyBox.Rendering
{
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
            private static readonly int s_MoonPosID = Shader.PropertyToID("_MoonPos");
            private static readonly int s_LightDirID = Shader.PropertyToID("_LightDir");
            private static readonly int s_SunColID = Shader.PropertyToID("_SunCol");
            private static readonly int s_SkyTintID = Shader.PropertyToID("_SkyTint");
            private static readonly int s_GroundColorID = Shader.PropertyToID("_GroundColor");
            private static readonly int s_SunSizeID = Shader.PropertyToID("_SunSize");
            private static readonly int s_SunSizeConvergenceID = Shader.PropertyToID("_SunSizeConvergence");
            private static readonly int s_MoonSizeID = Shader.PropertyToID("_MoonSize");
            private static readonly int s_MoonSizeConvergenceID = Shader.PropertyToID("_MoonSizeConvergence");
            private static readonly int s_AtmosphereThicknessID = Shader.PropertyToID("_AtmosphereThickness");
            private static readonly int s_ExposureID = Shader.PropertyToID("_Exposure");

            private static readonly int s_kDefaultScatteringWavelengthParamsID = Shader.PropertyToID("_kDefaultScatteringWavelength");
            private static readonly int s_kVariableRangeForScatteringWavelengthParamsID = Shader.PropertyToID("_kVariableRangeForScatteringWavelength");
            private static readonly int s_OUTER_RADIUSParamsID = Shader.PropertyToID("_OUTER_RADIUS");
            private static readonly int s_kInnerRadiusParamsID = Shader.PropertyToID("_kInnerRadius");
            private static readonly int s_kInnerRadius2ParamsID = Shader.PropertyToID("_kInnerRadius2");
            private static readonly int s_kCameraHeightParamsID = Shader.PropertyToID("_kCameraHeight");
            private static readonly int s_kRAYLEIGH_MAXParamsID = Shader.PropertyToID("_kRAYLEIGH_MAX");
            private static readonly int s_kRAYLEIGH_POWParamsID = Shader.PropertyToID("_kRAYLEIGH_POW");
            private static readonly int s_kMIEParamsID = Shader.PropertyToID("_kMIE");
            private static readonly int s_kSUN_BRIGHTNESSParamsID = Shader.PropertyToID("_kSUN_BRIGHTNESS");
            private static readonly int s_kMAX_SCATTERParamsID = Shader.PropertyToID("_kMAX_SCATTER");
            private static readonly int s_kHDSundiskIntensityFactorParamsID = Shader.PropertyToID("_kHDSundiskIntensityFactor");
            private static readonly int s_kSimpleSundiskIntensityFactorParamsID = Shader.PropertyToID("_kSimpleSundiskIntensityFactor");
            private static readonly int s_kSunScale_MultiplierParamsID = Shader.PropertyToID("_kSunScale_Multiplier");
            private static readonly int s_kKm4PI_MultiParamsID = Shader.PropertyToID("_kKm4PI_Multi");
            private static readonly int s_kScaleDepthParamsID = Shader.PropertyToID("_kScaleDepth");
            private static readonly int s_kScaleOverScaleDepth_MultiParamsID = Shader.PropertyToID("_kScaleOverScaleDepth_Multi");
            private static readonly int s_kSamplesParamsID = Shader.PropertyToID("_kSamples");
            private static readonly int s_MIE_GParamsID = Shader.PropertyToID("_MIE_G");
            private static readonly int s_MIE_G2ParamsID = Shader.PropertyToID("_MIE_G2");
            private static readonly int s_SKY_GROUND_THRESHOLDParamsID = Shader.PropertyToID("_SKY_GROUND_THRESHOLD");

            private static readonly int s_StarArraySRA0ID = Shader.PropertyToID("_starArraySRA0");
            private static readonly int s_StarArraySDEC0ID = Shader.PropertyToID("_starArraySDEC0");

            private readonly TimeOfDayRenderingModel timeOfDayRenderingModel;

            // Statics
            //private static readonly int s_SkyBoxCubemapTextureID = Shader.PropertyToID(k_SkyBoxCubemapTextureName);

            // Debug
            private readonly ReportData m_ReportData = new ("DCL_RenderPass_GenerateSkyBox", ReportDebounce.AssemblyStatic);

            private readonly int nDimensions = 4096;
            private readonly StarParam[] starList_ComputeBuffer;

            private Vector4 vSunPos;
            private Vector4 vMoonPos;
            private Vector4 vLightDir;

            //private int nArraySize = 6;
            private ComputeShader StarsComputeShader;
            private RTHandle CubemapTextureArray;
            private ComputeBuffer starBuffer;
            private Material m_Material_SkyBox_Generate;
            private Material m_Material_StarBox_Generate;
            private ProceduralSkyBoxSettings_Generate m_Settings_Generate;
            private RTHandle m_SkyBoxCubeMap_RTHandle;
            private RTHandle m_StarBoxCubeMap_RTHandle;

            private Transform directionalLight;

            private bool bComputeStarMap = false;

            internal DCL_RenderPass_GenerateSkyBox(TimeOfDayRenderingModel timeOfDayRenderingModel, bool _bComputeStarMap)
            {
                bComputeStarMap = _bComputeStarMap;
                bComputeStarMap = false;
                this.timeOfDayRenderingModel = timeOfDayRenderingModel;

                if (bComputeStarMap == true)
                {
                    var asset = Resources.Load("BSC5") as TextAsset;

                    if (asset != null)
                    {
                        var starlist = BSC5.Parse(asset.bytes);

                        starList_ComputeBuffer = new StarParam[starlist.Entries.Length];

                        for (var i = 0; i < starlist.Entries.Length; ++i)
                        {
                            starList_ComputeBuffer[i].XNO = starlist.Entries[i].XNO;
                            starList_ComputeBuffer[i].SRA0 = (float)starlist.Entries[i].SRA0;
                            starList_ComputeBuffer[i].SDEC0 = (float)starlist.Entries[i].SDEC0;

                            switch (starlist.Entries[i].IS[0])
                            {
                                case 'O':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.59f, 0.67f, 0.97f);
                                    break;
                                case 'B':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.63f, 0.73f, 0.96f);
                                    break;
                                case 'A':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.76f, 0.82f, 0.97f);
                                    break;
                                case 'F':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.94f, 0.93f, 0.95f);
                                    break;
                                case 'G':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.95f, 0.92f, 0.89f);
                                    break;
                                case 'K':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.95f, 0.79f, 0.61f);
                                    break;
                                case 'M':
                                    starList_ComputeBuffer[i].IS = new Vector3(0.97f, 0.78f, 0.42f);
                                    break;
                                default:
                                    starList_ComputeBuffer[i].IS = new Vector3(1.0f, 0.0f, 0.0f);
                                    break;
                            }

                            var fStarIntensity = 1.0f;
                            fStarIntensity = starlist.Entries[i].MAG * 100;
                            fStarIntensity = Mathf.Abs(fStarIntensity);
                            fStarIntensity = Mathf.Min(fStarIntensity, 128000.0f);
                            fStarIntensity = fStarIntensity / 128000.0f;
                            fStarIntensity = Mathf.Min(1.0f, fStarIntensity);
                            fStarIntensity = Mathf.Max(0.0f, fStarIntensity);
                            starList_ComputeBuffer[i].MAG = fStarIntensity;
                            starList_ComputeBuffer[i].XRPM = starlist.Entries[i].XRPM;
                            starList_ComputeBuffer[i].XDPM = starlist.Entries[i].XDPM;
                        }
                    }
                }
            }

            internal void SetStarsComputeShader(ComputeShader computeShader) =>
                StarsComputeShader = computeShader;

            internal void Setup(ProceduralSkyBoxSettings_Generate _featureSettings, Material _skyboxMaterial, Material _starboxMaterial, RTHandle _SkyBoxRTHandle, RTHandle _StarBoxRTHandle,
                ComputeShader _StarsComputeShader, RTHandle _CubemapTextureArray)
            {
                bComputeStarMap = false;
                m_Material_SkyBox_Generate = _skyboxMaterial;
                m_Material_StarBox_Generate = _starboxMaterial;
                m_Settings_Generate = _featureSettings;
                m_SkyBoxCubeMap_RTHandle = _SkyBoxRTHandle;
                m_StarBoxCubeMap_RTHandle = _StarBoxRTHandle;
                StarsComputeShader = _StarsComputeShader;
                CubemapTextureArray = _CubemapTextureArray;

                if (bComputeStarMap == true)
                {
                    if (starBuffer == null || !starBuffer.IsValid())
                    {
                        starBuffer = new ComputeBuffer(9110, Unsafe.SizeOf<StarParam>(), ComputeBufferType.Structured, ComputeBufferMode.Immutable);
                        starBuffer.SetData(starList_ComputeBuffer);
                    }
                }
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                vSunPos = timeOfDayRenderingModel.GetSunPosition();
                vSunPos = Vector3.Normalize(vSunPos);
                vMoonPos = timeOfDayRenderingModel.GetMoonPosition();
                vMoonPos = Vector3.Normalize(vMoonPos);
                vLightDir = timeOfDayRenderingModel.GetLightDirection();

                m_Material_SkyBox_Generate.SetVector(s_SunPosID, vSunPos);
                m_Material_SkyBox_Generate.SetVector(s_MoonPosID, vMoonPos);
                m_Material_SkyBox_Generate.SetVector(s_LightDirID, vLightDir);
                m_Material_SkyBox_Generate.SetVector(s_SunColID, m_Settings_Generate.SunColour);
                m_Material_SkyBox_Generate.SetVector(s_SkyTintID, m_Settings_Generate.SkyTint);
                m_Material_SkyBox_Generate.SetVector(s_GroundColorID, m_Settings_Generate.GroundColor);
                m_Material_SkyBox_Generate.SetFloat(s_SunSizeID, m_Settings_Generate.SunSize);
                m_Material_SkyBox_Generate.SetFloat(s_SunSizeConvergenceID, m_Settings_Generate.SunSizeConvergence);
                m_Material_SkyBox_Generate.SetFloat(s_MoonSizeID, m_Settings_Generate.MoonSize);
                m_Material_SkyBox_Generate.SetFloat(s_MoonSizeConvergenceID, m_Settings_Generate.MoonSizeConvergence);
                m_Material_SkyBox_Generate.SetFloat(s_AtmosphereThicknessID, m_Settings_Generate.AtmosphereThickness);
                m_Material_SkyBox_Generate.SetFloat(s_ExposureID, m_Settings_Generate.Exposure);

                m_Material_SkyBox_Generate.SetVector(s_kDefaultScatteringWavelengthParamsID, m_Settings_Generate._kDefaultScatteringWavelength);
                m_Material_SkyBox_Generate.SetVector(s_kVariableRangeForScatteringWavelengthParamsID, m_Settings_Generate._kVariableRangeForScatteringWavelength);
                m_Material_SkyBox_Generate.SetFloat(s_OUTER_RADIUSParamsID, m_Settings_Generate._OUTER_RADIUS);
                m_Material_SkyBox_Generate.SetFloat(s_kInnerRadiusParamsID, m_Settings_Generate._kInnerRadius);
                m_Material_SkyBox_Generate.SetFloat(s_kInnerRadius2ParamsID, m_Settings_Generate._kInnerRadius2);
                m_Material_SkyBox_Generate.SetFloat(s_kCameraHeightParamsID, m_Settings_Generate._kCameraHeight);
                m_Material_SkyBox_Generate.SetFloat(s_kRAYLEIGH_MAXParamsID, m_Settings_Generate._kRAYLEIGH_MAX);
                m_Material_SkyBox_Generate.SetFloat(s_kRAYLEIGH_POWParamsID, m_Settings_Generate._kRAYLEIGH_POW);
                m_Material_SkyBox_Generate.SetFloat(s_kMIEParamsID, m_Settings_Generate._kMIE);
                m_Material_SkyBox_Generate.SetFloat(s_kSUN_BRIGHTNESSParamsID, m_Settings_Generate._kSUN_BRIGHTNESS * timeOfDayRenderingModel.GetLightIntensity());
                m_Material_SkyBox_Generate.SetFloat(s_kMAX_SCATTERParamsID, m_Settings_Generate._kMAX_SCATTER);
                m_Material_SkyBox_Generate.SetFloat(s_kHDSundiskIntensityFactorParamsID, m_Settings_Generate._kHDSundiskIntensityFactor);
                m_Material_SkyBox_Generate.SetFloat(s_kSimpleSundiskIntensityFactorParamsID, m_Settings_Generate._kSimpleSundiskIntensityFactor);
                m_Material_SkyBox_Generate.SetFloat(s_kSunScale_MultiplierParamsID, m_Settings_Generate._kSunScale_Multiplier);
                m_Material_SkyBox_Generate.SetFloat(s_kKm4PI_MultiParamsID, m_Settings_Generate._kKm4PI_Multi);
                m_Material_SkyBox_Generate.SetFloat(s_kScaleDepthParamsID, m_Settings_Generate._kScaleDepth);
                m_Material_SkyBox_Generate.SetFloat(s_kScaleOverScaleDepth_MultiParamsID, m_Settings_Generate._kScaleOverScaleDepth_Multi);
                m_Material_SkyBox_Generate.SetFloat(s_kSamplesParamsID, m_Settings_Generate._kSamples);
                m_Material_SkyBox_Generate.SetFloat(s_MIE_GParamsID, m_Settings_Generate._MIE_G);
                m_Material_SkyBox_Generate.SetFloat(s_MIE_G2ParamsID, m_Settings_Generate._MIE_G2);
                m_Material_SkyBox_Generate.SetFloat(s_SKY_GROUND_THRESHOLDParamsID, m_Settings_Generate._SKY_GROUND_THRESHOLD);
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                // Configure targets and clear color
                ConfigureTarget(m_SkyBoxCubeMap_RTHandle);
                // if (bComputeStarMap == true)
                //     ConfigureTarget(m_StarBoxCubeMap_RTHandle);
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

                bComputeStarMap = false;
                if (bComputeStarMap == true)
                {
                    if (m_Material_StarBox_Generate == null)
                    {
                        ReportHub.LogError(m_ReportData, $"{GetType().Name}.Execute(): Missing material. DCL_RenderPass_GenerateSkyBox pass will not execute. Check for missing reference in the renderer resources.");
                        return;
                    }

                    CommandBuffer cmdStarBox = CommandBufferPool.Get();

                    using (new ProfilingScope(cmdStarBox, new ProfilingSampler(profilerStarBoxTag)))
                    {
                        var kernelName = "CSMain";
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

                    CommandBuffer cmdStarBoxCopy = CommandBufferPool.Get();

                    using (new ProfilingScope(cmdStarBoxCopy, new ProfilingSampler(profilerStarBoxTag)))
                    {
                        cmdStarBoxCopy.SetGlobalTexture("_CubemapTextureArray", CubemapTextureArray);
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveX, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null);

                        //cmd.SetGlobalVector(s_ParamsID, new Vector4(1.0f, 0.0f, 0.0f, 0.0f));
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeX, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Left);

                        //cmd.SetGlobalVector(s_ParamsID, new Vector4(2.0f, 0.0f, 0.0f, 0.0f));
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveY, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Up);

                        //cmd.SetGlobalVector(s_ParamsID, new Vector4(3.0f, 0.0f, 0.0f, 0.0f));
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeY, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Down);

                        //cmd.SetGlobalVector(s_ParamsID, new Vector4(4.0f, 0.0f, 0.0f, 0.0f));
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveZ, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Front);

                        //cmd.SetGlobalVector(s_ParamsID, new Vector4(5.0f, 0.0f, 0.0f, 0.0f));
                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, buffer: m_StarBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeZ, depthSlice: 0);
                        CoreUtils.DrawFullScreen(cmdStarBoxCopy, m_Material_StarBox_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Back);

                        CoreUtils.SetRenderTarget(cmdStarBoxCopy, CubemapTextureArray);
                        CoreUtils.ClearRenderTarget(cmdStarBoxCopy, ClearFlag.Color, Color.clear);
                    }

                    context.ExecuteCommandBuffer(cmdStarBoxCopy);
                    CommandBufferPool.Release(cmdStarBoxCopy);
                }

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
                    CoreUtils.DrawFullScreen(cmdSkyBox, m_Material_SkyBox_Generate, properties: null);

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

            public void Dispose()
            {
                m_SkyBoxCubeMap_RTHandle?.Release();

                if (bComputeStarMap == true)
                {
                    m_StarBoxCubeMap_RTHandle?.Release();
                    CubemapTextureArray?.Release();
                    starBuffer?.Release();
                }
            }

            private struct StarParam
            {
                public float XNO; // Catalog number of star
                public float SRA0; // B1950 Right Ascension (radians)
                public float SDEC0; // B1950 Declination (radians)
                public Vector3 IS; // Spectral type (2 characters)
                public float MAG; // V Magnitude * 100
                public float XRPM; // R.A. proper motion (radians per year)
                public float XDPM; // Dec. proper motion (radians per year)
            }
        }
    }
}
