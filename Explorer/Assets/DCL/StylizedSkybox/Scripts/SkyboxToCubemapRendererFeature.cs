using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.StylizedSkybox.Scripts
{
    public class SkyboxToCubemapRendererFeature : ScriptableRendererFeature
    {
        public enum TimeSlicingMode
        {
            AllFacesAtOnce,
            OneFacePerFrame,
        }

        [Serializable]
        public class SkyBoxToCubemapSettings
        {
            public Material skyBoxMaterial;
            public TimeSlicingMode timeSlicingMode;
            public bool executeInEditMode;
            public bool assignAsReflectionProbe;
        }

        public SkyBoxToCubemapSettings settings = new ();

        private const int SKY_BOX_DIMENSIONS = 256;

        private RTHandle skyBoxCubeMapRTHandle;

        private RenderPass renderPass;

        public override void Create()
        {
            // Create is called on enable and on validate (from Editor)
            if (settings.skyBoxMaterial == null)
                return;

            // If render texture was created but no longer should be executed in the editor release it
            if (SkipInEditorMode() && skyBoxCubeMapRTHandle != null)
            {
                skyBoxCubeMapRTHandle.Release();
                skyBoxCubeMapRTHandle = null;
            }

            if (SkipInEditorMode())
                return;

            // Create renderTexture cubeMap

            var desc = new RenderTextureDescriptor();
            desc.autoGenerateMips = false;
            desc.bindMS = false;
            desc.colorFormat = RenderTextureFormat.ARGB32;
            desc.depthBufferBits = 0;
            desc.depthStencilFormat = GraphicsFormat.None;
            desc.dimension = TextureDimension.Cube;
            desc.enableRandomWrite = false;
            desc.graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm;
            desc.height = SKY_BOX_DIMENSIONS;
            desc.width = SKY_BOX_DIMENSIONS;
            desc.memoryless = RenderTextureMemoryless.None;
            desc.mipCount = 0;
            desc.msaaSamples = 1;
            desc.shadowSamplingMode = ShadowSamplingMode.None;
            desc.sRGB = true;
            desc.stencilFormat = GraphicsFormat.None;
            desc.useDynamicScale = false;
            desc.useMipMap = false;
            desc.volumeDepth = 0;
            desc.vrUsage = VRTextureUsage.None;

            RenderingUtils.ReAllocateIfNeeded(ref skyBoxCubeMapRTHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                isShadowMap: false, anisoLevel: 1, mipMapBias: 0F, name: "_SkyBoxCubeMapTex");

            if (settings.assignAsReflectionProbe)
            {
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflectionTexture = skyBoxCubeMapRTHandle;
            }

            renderPass = new RenderPass(skyBoxCubeMapRTHandle, settings.timeSlicingMode, settings.skyBoxMaterial);
        }

        protected override void Dispose(bool disposing)
        {
            skyBoxCubeMapRTHandle?.Release();
            skyBoxCubeMapRTHandle = null;
        }

        private bool SkipInEditorMode() =>
            !settings.executeInEditMode && Application.isEditor && !Application.isPlaying;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (SkipInEditorMode())
                return;

            renderer.EnqueuePass(renderPass);
        }

        public class RenderPass : ScriptableRenderPass
        {
            private static readonly IReadOnlyList<CubemapFace> FACES =
                ((CubemapFace[])Enum.GetValues(typeof(CubemapFace))).Where(c => c != CubemapFace.Unknown).ToList();

            private const string CUBE_MAP_FACE_PROP = "_CubemapFace";
            private readonly int cubeMapFacePropId = Shader.PropertyToID(CUBE_MAP_FACE_PROP);

            private readonly MaterialPropertyBlock materialPropertyBlock = new ();

            private readonly RTHandle skyBoxCubeMapRTHandle;
            private readonly Material material;
            private readonly ProfilingSampler profilingSampler;
            private readonly TimeSlicingMode timeSlicingMode;

            public RenderPass(RTHandle skyBoxCubeMapRTHandle, TimeSlicingMode timeSlicingMode, Material material)
            {
                this.skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
                this.timeSlicingMode = timeSlicingMode;
                this.material = material;

                renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
                profilingSampler = new ProfilingSampler($"{nameof(SkyboxToCubemapRendererFeature)}.{nameof(RenderPass)}");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Modify camera data: aspect, FOV

                const float ASPECT = 1.0f;
                const float FOV = 90f;

                CommandBuffer cmd = CommandBufferPool.Get("SkyBoxToCubemapPass");
                using var _ = new ProfilingScope(cmd, profilingSampler);

                ref CameraData cameraData = ref renderingData.cameraData;

                var projectionMatrix = Matrix4x4.Perspective(FOV, ASPECT, 1, 2); // we don't care about objects
                projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());
                Matrix4x4 viewMatrix = cameraData.GetViewMatrix();

                // consult ShaderPropertyId for built-in names for global shader variables
                RenderingUtils.SetViewAndProjectionMatrices(cmd, projectionMatrix, viewMatrix, false);

                // Draw 6 faces of the cube map

                foreach (CubemapFace face in FACES)
                {
                    // Set render target
                    CoreUtils.SetRenderTarget(cmd, skyBoxCubeMapRTHandle, ClearFlag.None, Color.black, 0, face);

                    // Do we need to clear Render Target?

                    materialPropertyBlock.SetFloat(cubeMapFacePropId, (int)face);

                    // Draw fullscreen triangle
                    CoreUtils.DrawFullScreen(cmd, material, materialPropertyBlock);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
