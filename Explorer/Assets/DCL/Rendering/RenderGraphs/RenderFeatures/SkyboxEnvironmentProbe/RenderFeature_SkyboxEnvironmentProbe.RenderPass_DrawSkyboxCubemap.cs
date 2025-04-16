using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.SkyboxEnvironmentProbe
{
    public class RenderPass_DrawSkyboxCubemap : ScriptableRenderPass
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

        public RTHandle m_skyBoxCubeMapRTHandle;

        private class CubeMapGenerationPassData
        {
            internal MaterialPropertyBlock materialPropertyBlock;
            internal Material newMaterial;
            internal Material originalMaterial;
            internal RTHandle skyBoxCubeMapRTHandle;
        }

        public RenderPass_DrawSkyboxCubemap(Material material, Material originalMaterial)
        {
            this.material = material;
            this.originalMaterial = originalMaterial;


            profilingSampler = new ProfilingSampler($"{nameof(RendererFeature_SkyboxEnvironmentProbe)}.{nameof(RenderPass_DrawSkyboxCubemap)}");

            // It looks like upon Rendering Unity compensates UV Flip automatically
            materialPropertyBlock.SetFloat(flipCompensatedPropId, 1);
        }

        internal void Setup(RTHandle skyBoxCubeMapRTHandle)
        {
            this.m_skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (!material || !originalMaterial)
                return;

            // Copy properties from the original material as it is being constantly modified
            material.CopyPropertiesFromMaterial(originalMaterial);

            using (var builder = renderGraph.AddUnsafePass<CubeMapGenerationPassData>("SkyboxCubeMapGeneration", out var passData))
            {
                // Configure pass data
                passData.materialPropertyBlock = materialPropertyBlock;
                passData.newMaterial = material;
                passData.originalMaterial = originalMaterial;
                passData.skyBoxCubeMapRTHandle = m_skyBoxCubeMapRTHandle;
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((CubeMapGenerationPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Set render target
                    context.cmd.SetRenderTarget(data.skyBoxCubeMapRTHandle);

                    // Draw 6 faces of the cube map
                    for (var index = 0; index < FACES.Count; index++)
                    {
                        CubemapFace face = FACES[index];
                        
                        context.cmd.SetRenderTarget(data.skyBoxCubeMapRTHandle, 0, face);
                        //data.materialPropertyBlock.SetFloat(cubeMapFacePropId, (int)face);
                        data.newMaterial.SetFloat(cubeMapFacePropId, (int)face);
                        // Draw fullscreen triangle
                        //CoreUtils.DrawFullScreen(cmd, data.newMaterial, data.materialPropertyBlock);
                        CoreUtils.DrawFullScreen(cmd, data.newMaterial);
                    }
                });
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {

        }
    }
}
