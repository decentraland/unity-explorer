using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.StylizedSkybox.Scripts
{
    public class SkyboxToCubemapRenderPass : ScriptableRenderPass
    {
        private const string CUBE_MAP_FACE_PROP = "_CubemapFace";
        private const string FLIP_COMPENSATED_PROP = "_UVFlipCompensated";

        private static readonly IReadOnlyList<CubemapFace> FACES =
            ((CubemapFace[])Enum.GetValues(typeof(CubemapFace))).Where(c => c != CubemapFace.Unknown).ToList();

        private readonly int cubeMapFacePropId = Shader.PropertyToID(CUBE_MAP_FACE_PROP);
        private readonly int flipCompensatedPropId = Shader.PropertyToID(FLIP_COMPENSATED_PROP);

        private readonly MaterialPropertyBlock materialPropertyBlock = new ();

        private readonly Material material;
        private readonly Material originalMaterial;
        private readonly ProfilingSampler profilingSampler;

        private RTHandle skyBoxCubeMapRTHandle;

        public SkyboxToCubemapRenderPass(Material material, Material originalMaterial)
        {
            this.material = material;
            this.originalMaterial = originalMaterial;

            renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            profilingSampler = new ProfilingSampler($"{nameof(SkyboxToCubemapRendererFeature)}.{nameof(SkyboxToCubemapRenderPass)}");

            // It looks like upon Rendering Unity compensates UV Flip automatically
            materialPropertyBlock.SetFloat(flipCompensatedPropId, 1);
        }

        internal void Setup(RTHandle skyBoxCubeMapRTHandle)
        {
            this.skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!material || !originalMaterial) return;

            // Copy properties from the original material as it is being constantly modified
            material.CopyPropertiesFromMaterial(originalMaterial);

            CommandBuffer cmd = CommandBufferPool.Get("SkyBoxToCubemapPass");

            using (new ProfilingScope(cmd, profilingSampler))
            {
                // Draw 6 faces of the cube map

                for (var index = 0; index < FACES.Count; index++)
                {
                    CubemapFace face = FACES[index];

                    // Set render target
                    CoreUtils.SetRenderTarget(cmd, skyBoxCubeMapRTHandle, ClearFlag.None, Color.green, 0, face);

                    materialPropertyBlock.SetFloat(cubeMapFacePropId, (int)face);

                    // Draw fullscreen triangle
                    CoreUtils.DrawFullScreen(cmd, material, materialPropertyBlock);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
