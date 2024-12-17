using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.StylizedSkybox.Scripts
{
    public class SkyboxToCubemapRendererFeature : ScriptableRendererFeature
    {
        public SkyBoxToCubemapSettings settings = new ();

        private RenderTextureDescriptor desc;
        private RenderTextureDescriptor descScratch;
        private SkyboxToCubemapRenderPass renderPass;
        private Material skyBoxMaterial;
        private Material cubeCopyMaterial;
        private Material cubeBlurMaterial;

        private RTHandle skyBoxCubeMapRTHandle;
        private RTHandle skyBoxCubeMapRTHandle_Scratch;

        public override void Create()
        {
            // Create is called on enable and on validate (from Editor)
            if (settings.skyBoxShader == null || settings.originalMaterial == null)
                return;

            if (!Mathf.IsPowerOfTwo(settings.dimensions)) return;

            CoreUtils.Destroy(skyBoxMaterial); // destroy previously created material
            skyBoxMaterial = new Material(settings.skyBoxShader);

            CoreUtils.Destroy(cubeCopyMaterial); // destroy previously created material
            cubeCopyMaterial = new Material(Shader.Find("DCL/CubeCopy"));

            CoreUtils.Destroy(cubeBlurMaterial); // destroy previously created material
            cubeBlurMaterial = new Material(Shader.Find("DCL/CubeBlur"));

            // If render texture was created but no longer should be executed in the editor release it
            if (SkipInEditorMode())
            {
                if (skyBoxCubeMapRTHandle != null)
                {
                    skyBoxCubeMapRTHandle.Release();
                    skyBoxCubeMapRTHandle = null;
                }

                if (skyBoxCubeMapRTHandle_Scratch != null)
                {
                    skyBoxCubeMapRTHandle_Scratch.Release();
                    skyBoxCubeMapRTHandle_Scratch = null;
                }
            }

            if (SkipInEditorMode())
                return;

            // Create renderTexture cubeMap

            desc = new RenderTextureDescriptor();
            desc.autoGenerateMips = false;
            desc.bindMS = false;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.depthBufferBits = 0;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.dimension = TextureDimension.Cube;
            desc.enableRandomWrite = false;
            desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            desc.height = settings.dimensions;
            desc.width = settings.dimensions;
            desc.memoryless = RenderTextureMemoryless.None;
            // desc.mipCount = 0;
            desc.msaaSamples = 1;
            desc.shadowSamplingMode = ShadowSamplingMode.None;
            desc.sRGB = false;
            desc.stencilFormat = GraphicsFormat.None;
            desc.useDynamicScale = false;
            desc.useMipMap = true;
            desc.volumeDepth = 0;
            desc.vrUsage = VRTextureUsage.None;

            descScratch = new RenderTextureDescriptor();
            descScratch.autoGenerateMips = false;
            descScratch.bindMS = false;
            descScratch.colorFormat = RenderTextureFormat.ARGB32;
            descScratch.depthBufferBits = 0;
            descScratch.depthStencilFormat = GraphicsFormat.None;
            descScratch.dimension = TextureDimension.Cube;
            descScratch.enableRandomWrite = false;
            descScratch.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            descScratch.height = settings.dimensions;
            descScratch.width = settings.dimensions;
            descScratch.memoryless = RenderTextureMemoryless.None;
            // descScratch.mipCount = 0;
            descScratch.msaaSamples = 1;
            descScratch.shadowSamplingMode = ShadowSamplingMode.None;
            descScratch.sRGB = false;
            descScratch.stencilFormat = GraphicsFormat.None;
            descScratch.useDynamicScale = false;
            descScratch.useMipMap = true;
            descScratch.volumeDepth = 0;
            descScratch.vrUsage = VRTextureUsage.None;

            renderPass = new SkyboxToCubemapRenderPass(skyBoxMaterial, settings.originalMaterial, cubeCopyMaterial, cubeBlurMaterial);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (SkipInEditorMode())
                return;

            bool bAllocatedSkyBoxCubeMapRT = RenderingUtils.ReAllocateIfNeeded(ref skyBoxCubeMapRTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex");

            if (bAllocatedSkyBoxCubeMapRT)
            {
                if (settings.assignAsReflectionProbe)
                {
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = skyBoxCubeMapRTHandle;
                }
            }

            bool bAllocatedSkyBoxCubeMapRT_Scratch = RenderingUtils.ReAllocateIfNeeded(ref skyBoxCubeMapRTHandle_Scratch, descScratch, FilterMode.Bilinear, TextureWrapMode.Clamp,
                isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex_Scratch");

            if (bAllocatedSkyBoxCubeMapRT_Scratch)
            {
                if (settings.assignAsReflectionProbe)
                {
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = skyBoxCubeMapRTHandle;
                }
            }

            if (bAllocatedSkyBoxCubeMapRT)
                renderPass.Setup(skyBoxCubeMapRTHandle, skyBoxCubeMapRTHandle_Scratch);
        }

        protected override void Dispose(bool disposing)
        {
            skyBoxCubeMapRTHandle?.Release();
            skyBoxCubeMapRTHandle = null;

            skyBoxCubeMapRTHandle_Scratch?.Release();
            skyBoxCubeMapRTHandle_Scratch = null;

            CoreUtils.Destroy(skyBoxMaterial);
            skyBoxMaterial = null;

            CoreUtils.Destroy(cubeCopyMaterial);
            cubeCopyMaterial = null;

            CoreUtils.Destroy(cubeBlurMaterial);
            cubeBlurMaterial = null;
        }

        private bool SkipInEditorMode() =>
            !settings.executeInEditMode && Application.isEditor && !Application.isPlaying;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (SkipInEditorMode())
                return;

            if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.SceneView
                || !renderingData.cameraData.camera.CompareTag("MainCamera"))
                return;

            if (!skyBoxMaterial)
                return;

            if (!cubeCopyMaterial)
                return;

            if (!cubeBlurMaterial)
                return;

            renderer.EnqueuePass(renderPass);
        }

        [Serializable]
        public class SkyBoxToCubemapSettings
        {
            public Shader skyBoxShader;
            public Material originalMaterial;
            public int dimensions = 256;
            public bool executeInEditMode;
            public bool assignAsReflectionProbe;
        }
    }
}
