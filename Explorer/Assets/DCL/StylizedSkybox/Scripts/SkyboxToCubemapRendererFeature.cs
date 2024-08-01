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
        private SkyboxToCubemapRenderPass renderPass;
        private Material skyBoxMaterial;

        private RTHandle skyBoxCubeMapRTHandle;

        public override void Create()
        {
            // Create is called on enable and on validate (from Editor)
            if (settings.skyBoxShader == null || settings.originalMaterial == null)
                return;

            if (!Mathf.IsPowerOfTwo(settings.dimensions)) return;

            CoreUtils.Destroy(skyBoxMaterial); // destroy previously created material
            skyBoxMaterial = new Material(settings.skyBoxShader);

            // If render texture was created but no longer should be executed in the editor release it
            if (SkipInEditorMode() && skyBoxCubeMapRTHandle != null)
            {
                skyBoxCubeMapRTHandle.Release();
                skyBoxCubeMapRTHandle = null;
            }

            if (SkipInEditorMode())
                return;

            // Create renderTexture cubeMap

            desc = new RenderTextureDescriptor();
            desc.autoGenerateMips = true;
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
            desc.mipCount = 0;
            desc.msaaSamples = 1;
            desc.shadowSamplingMode = ShadowSamplingMode.None;
            desc.sRGB = false;
            desc.stencilFormat = GraphicsFormat.None;
            desc.useDynamicScale = false;
            desc.useMipMap = true;
            desc.volumeDepth = 0;
            desc.vrUsage = VRTextureUsage.None;

            renderPass = new SkyboxToCubemapRenderPass(skyBoxMaterial, settings.originalMaterial);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            if (SkipInEditorMode())
                return;

            if (RenderingUtils.ReAllocateIfNeeded(ref skyBoxCubeMapRTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex"))
            {
                if (settings.assignAsReflectionProbe)
                {
                    RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = skyBoxCubeMapRTHandle;
                }

                renderPass.Setup(skyBoxCubeMapRTHandle);
            }
        }

        protected override void Dispose(bool disposing)
        {
            skyBoxCubeMapRTHandle?.Release();
            skyBoxCubeMapRTHandle = null;

            CoreUtils.Destroy(skyBoxMaterial);
            skyBoxMaterial = null;
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
