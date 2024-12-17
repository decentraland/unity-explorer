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
        private readonly Material cubeCopyMaterial;
        private readonly Material cubeBlurMaterial;
        private readonly ProfilingSampler profilingSampler;
        private readonly ProfilingSampler profilingSampler_convolution;

        private RTHandle skyBoxCubeMapRTHandle;
        private RTHandle skyBoxCubeMapRTHandle_Scratch;
        private int skyBoxCubeMapWidth = 256;
        private int skyBoxCubeMapHeight = 256;

        // Copy shader vars
        private readonly int kSLPropMainTex_copy;
        private readonly int kSLPropLevel_copy;
        // Blur shader vars
        private readonly int kSLPropMainTex_blur;
        private readonly int kSLPropTexel_blur;
        private readonly int kSLPropLevel_blur;
        private readonly int kSLPropScale_blur;
        private readonly int kSLPropCurrentCubeFace_blur;

        public SkyboxToCubemapRenderPass(Material material, Material originalMaterial, Material cubeCopyMaterial, Material cubeBlurMaterial)
        {
            this.material = material;
            this.originalMaterial = originalMaterial;
            this.cubeCopyMaterial = cubeCopyMaterial;
            this.cubeBlurMaterial = cubeBlurMaterial;

            // Copy shader vars
            int temp = cubeCopyMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropMainTex_copy = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            temp = cubeCopyMaterial.shader.FindPropertyIndex("_Level");
            kSLPropLevel_copy = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            // // Blur shader vars
            temp = cubeBlurMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropMainTex_blur = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            temp = cubeBlurMaterial.shader.FindPropertyIndex("_TexelSize");
            kSLPropTexel_blur = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            temp = cubeBlurMaterial.shader.FindPropertyIndex("_MipLevel");
            kSLPropLevel_blur = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            temp = cubeBlurMaterial.shader.FindPropertyIndex("_BlurScale");
            kSLPropScale_blur = cubeBlurMaterial.shader.GetPropertyNameId(temp);
            temp = cubeBlurMaterial.shader.FindPropertyIndex("_Current_CubeFace");
            kSLPropCurrentCubeFace_blur = cubeBlurMaterial.shader.GetPropertyNameId(temp);

            renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            profilingSampler = new ProfilingSampler($"{nameof(SkyboxToCubemapRendererFeature)}.{nameof(SkyboxToCubemapRenderPass)}");
            profilingSampler_convolution = new ProfilingSampler("SkyboxToCubemapRenderPass_convolution");

            // It looks like upon Rendering Unity compensates UV Flip automatically
            materialPropertyBlock.SetFloat(flipCompensatedPropId, 1);
        }

        internal void Setup(RTHandle skyBoxCubeMapRTHandle, RTHandle skyBoxCubeMapRTHandle_Scratch)
        {
            this.skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
            this.skyBoxCubeMapRTHandle_Scratch = skyBoxCubeMapRTHandle_Scratch;
        }

        public int WidthOf(int level)
        {
            return 1 << (level + 1);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!material || !originalMaterial || !cubeCopyMaterial || !cubeBlurMaterial)
                return;

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

                cmd.GenerateMips(skyBoxCubeMapRTHandle);

                for (var index = 0; index < FACES.Count; index++)
                {
                    cmd.CopyTexture(skyBoxCubeMapRTHandle, index, 0, skyBoxCubeMapRTHandle_Scratch, index, 0);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            CommandBuffer cmd_conv = CommandBufferPool.Get("SkyBoxToCubemapPass_convolution");

            using (new ProfilingScope(cmd_conv, profilingSampler_convolution))
            {
                MaterialPropertyBlock tempMatPropBlock = new MaterialPropertyBlock();
                tempMatPropBlock.SetFloat(kSLPropScale_blur, 1.0f);

                int mipCount = 9;
                int size = skyBoxCubeMapWidth >> 1;
                float texelSize = 1.0f / size; // should be 2.f/size, but size is already divided by two

                for (int mipIndex = 1; mipIndex < mipCount; ++mipIndex)
                {
                    for (int nFaceIndex = 0; nFaceIndex < FACES.Count; ++nFaceIndex)
                    {
                        CubemapFace face = FACES[nFaceIndex];
                        CoreUtils.SetRenderTarget(cmd_conv, skyBoxCubeMapRTHandle_Scratch, ClearFlag.None, Color.green, mipIndex, face);

                        cmd_conv.SetGlobalTexture("_MainTex", skyBoxCubeMapRTHandle);
                        //cmd_conv.SetGlobalFloat(kSLPropCurrentCubeFace_blur, nFaceIndex);

                        tempMatPropBlock.SetFloat(kSLPropTexel_blur, texelSize);
                        // Output mip range -> normalized range -> input mip range

                        float level = mipIndex - 1.0f;
                        tempMatPropBlock.SetFloat(kSLPropLevel_blur, level);

                        tempMatPropBlock.SetFloat(kSLPropCurrentCubeFace_blur, nFaceIndex);

                        //CoreUtils.DrawFullScreen(cmd_conv, cubeBlurMaterial);
                        CoreUtils.DrawFullScreen(cmd_conv, cubeBlurMaterial, tempMatPropBlock);
                    }

                    texelSize *= 2;
                }
            }

            context.ExecuteCommandBuffer(cmd_conv);
            CommandBufferPool.Release(cmd_conv);

            // {
            //     const int specularSteps = 7; // MM: using 7 instead of lod since this is what the baked probes use regardless of resolution (look for m_CubemapConvolutionSteps).
            //     float step = 1.0f / (float)(specularSteps > 1 ? specularSteps - 1 : 1);
            //
            //     float roughness = step;
            //     int cubeMapWidth = 256;
            //
            //     for (int mipIndex = 1; mipIndex <= mipCount; ++mipIndex)
            //     {
            //         for (int nFaceIndex = 0; nFaceIndex < FACES.Count; ++nFaceIndex)
            //         {
            //             CubemapFace face = FACES[nFaceIndex];
            //
            //             // MM: original power was 1.5. I changed it to make blur strengths similar to baked probes
            //             float width = Mathf.Pow(roughness, 1.9f) * (2 * cubeMapWidth);
            //
            //             int level;
            //             float f;
            //
            //             if (size > 1)
            //             {
            //                 level = 7;
            //                 float n0;
            //
            //                 while ((n0 = WidthOf(level)) > width) { --level; }
            //
            //                 float n1 = WidthOf(level + 1);
            //                 f = (width - n0) / (n1 - n0);
            //             }
            //             else
            //             {
            //                 level = 7;
            //                 f = 0f;
            //             }
            //
            //             cubeCopyMaterial.SetTexture("_MainTex", skyBoxCubeMapRTHandle_Scratch);
            //             cubeCopyMaterial.SetFloat("_Level", level + f);
            //
            //             CoreUtils.SetRenderTarget(cmd, skyBoxCubeMapRTHandle, ClearFlag.None, Color.green, mipIndex, face);
            //             CoreUtils.DrawFullScreen(cmd, cubeCopyMaterial);
            //
            //             //CubemapBlit(inputCubemap, copyMaterail, 0, mipIndex, z);
            //             roughness += step;
            //             size >>= 1;
            //
            //             //z -= zStep;
            //         }
            //     }
            // }
        }
    }
}
