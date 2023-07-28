using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public partial class DCL_RenderFeature_ProceduralSkyBox : ScriptableRendererFeature
{
    public class DCL_RenderPass_GenerateSkyBox : ScriptableRenderPass
    {
        // public static RTHandle k_CameraTarget
        // public Color clearColor { get; }
        // public ClearFlag clearFlag { get; }
        // public RTHandle colorAttachmentHandle { get; }
        // public RTHandle[] colorAttachmentHandles { get; }
        // public RenderBufferStoreAction[] colorStoreActions { get; }
        // public RTHandle depthAttachmentHandle { get; }
        // public RenderBufferStoreAction depthStoreAction { get; }
        // public ScriptableRenderPassInput input { get; }
        // protected ProfilingSampler profilingSampler { get; set; }
        // public RenderPassEvent renderPassEvent { get; set; }

        // Camera m_Camera;

        const string profilerTag = "Custom Pass: GenerateSkyBox";
        private Material m_Material_Generate;
        private ProceduralSkyBoxSettings_Generate m_Settings_Generate;
        private Matrix4x4 viewMatrix;
        private Matrix4x4 projMatrix;
        RTHandle m_SkyBoxCubeMap_RTHandle;
        private static readonly int s_ParamsID = Shader.PropertyToID("_CurrentCubeFace");

        // Constants
        private const string k_SkyBoxCubemapTextureName = "_SkyBox_Cubemap_Texture";

        // Statics
        private static readonly int s_SkyBoxCubemapTextureID = Shader.PropertyToID(k_SkyBoxCubemapTextureName);

        
        private enum ShaderPasses
        {
            CubeMapFace_Front = 0,
            CubeMapFace_Left = 1,
            CubeMapFace_Back = 2,
            CubeMapFace_Right = 3,
            CubeMapFace_Up = 4,
            CubeMapFace_Down = 5
        }

        internal DCL_RenderPass_GenerateSkyBox()
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::Constructor");
            Vector3 from = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 to = new Vector3(1.0f, 0.0f, 0.0f);
            Vector3 up = new Vector3(0.0f, 0.0f, 1.0f);
            viewMatrix = Matrix4x4.LookAt(from, to, up);

            float fov = 90.0f;
            float aspect = 1.0f;
            float zNear = 1.0f;
            float zFar = 1000.0f;
            projMatrix = Matrix4x4.Perspective(fov, aspect, zNear, zFar);
        }

        internal void Setup(ProceduralSkyBoxSettings_Generate _featureSettings, Material _material, RTHandle _RTHandle)
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::Setup");
            this.m_Material_Generate = _material;
            this.m_Settings_Generate = _featureSettings;
            this.m_SkyBoxCubeMap_RTHandle = _RTHandle;

            //ConfigureInput(ScriptableRenderPassInput.Color);
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::OnCameraSetup");
        }

        //delegate RTHandle CreateBufferRTHandle_Method(RTHandleSystem _RTHandleSystem, int _nBufferId);

        // public RTHandle CreateBufferRTHandle(RTHandleSystem _RTHandleSystem, int _nBufferId)
        // {
        //     Debug.Log("DCL_RenderPass_GenerateSkyBox::BufferRTHandle");
        //     //RTHandle testHandle = new RTHandle();
        //     Texture myTexture;
        //     RTHandle myRTHandle = _RTHandleSystem.Alloc(myTexture);
        //     return myRTHandle;
        // }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::Configure");

            // Configure targets and clear color
            ConfigureTarget(this.m_SkyBoxCubeMap_RTHandle);
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::Execute");
            if (this.m_Material_Generate == null)
            {
                Debug.LogErrorFormat("{0}.Execute(): Missing material. DCL_RenderPass_GenerateSkyBox pass will not execute. Check for missing reference in the renderer resources.", GetType().Name);
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, new ProfilingSampler(profilerTag)))
            {
                //MaterialPropertyBlock properties
                CoreUtils.ClearCubemap(cmd, this.m_SkyBoxCubeMap_RTHandle.rt , Color.blue, clearMips : false);
                cmd.SetGlobalInt(s_ParamsID, 0);
                Debug.Log(s_ParamsID);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Right);

                cmd.SetGlobalInt(s_ParamsID, 1);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeX, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Left);

                cmd.SetGlobalInt(s_ParamsID, 2);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Up);

                cmd.SetGlobalInt(s_ParamsID, 3);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeY, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Down);

                cmd.SetGlobalInt(s_ParamsID, 4);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.PositiveZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Front);

                
                cmd.SetGlobalInt(s_ParamsID, 5);
                CoreUtils.SetRenderTarget(cmd, buffer: this.m_SkyBoxCubeMap_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.NegativeZ, depthSlice: 0);
                CoreUtils.DrawFullScreen(cmd, this.m_Material_Generate, properties: null, (int)ShaderPasses.CubeMapFace_Back);                
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        // Cleanup any allocated resources that were created during the execution of this render pass.
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::OnCameraCleanup");
        }

        public void dispose()
        {
            Debug.Log("DCL_RenderPass_GenerateSkyBox::dispose");
            this.m_SkyBoxCubeMap_RTHandle?.Release();
        }

        // public void Blit(CommandBuffer cmd, RTHandle source, RTHandle destination, Material material = null, int passIndex = 0) {}
        // public void Blit(CommandBuffer cmd, ref RenderingData data, Material material, int passIndex = 0) {}
        // public void Blit(CommandBuffer cmd, ref RenderingData data, RTHandle source, Material material, int passIndex = 0) {}
        // public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor) {}
        // public void ConfigureClear(ClearFlag clearFlag, Color clearColor) {}
        // public void ConfigureColorStoreAction(RenderBufferStoreAction storeAction, uint attachmentIndex = 0U) {}
        // public void ConfigureColorStoreActions(RenderBufferStoreAction[] storeActions) {}
        // public void ConfigureDepthStoreAction(RenderBufferStoreAction storeAction) {}
        // public void ConfigureInput(ScriptableRenderPassInput passInput) {}
        // public void ConfigureTarget(RTHandle colorAttachment) {}
        // public void ConfigureTarget(RTHandle colorAttachment, RTHandle depthAttachment) {}
        // public void ConfigureTarget(RTHandle[] colorAttachments) {}
        // public void ConfigureTarget(RTHandle[] colorAttachments, RTHandle depthAttachment) {}
        // public DrawingSettings CreateDrawingSettings(List<ShaderTagId> shaderTagIdList, ref RenderingData renderingData, SortingCriteria sortingCriteria) {}
        // public DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, ref RenderingData renderingData, SortingCriteria sortingCriteria) {}
        // public override void OnFinishCameraStackRendering(CommandBuffer cmd) {}
        // public void ResetTarget() {}
    }
}