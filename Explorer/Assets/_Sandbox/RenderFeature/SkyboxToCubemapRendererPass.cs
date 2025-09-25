using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

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
        private readonly Material cubeCopyMaterial;
        private readonly Material cubeBlurMaterial;
        private readonly ProfilingSampler profilingSampler;
        private readonly ProfilingSampler profilingSampler_convolution;
        private readonly ProfilingSampler profilingSampler_remapping;

        public RTHandle skyBoxCubeMapRTHandle;
        public RTHandle skyBoxCubeMapRTHandle_Scratch;
        private Material skyboxMaterial;
        private int skyBoxCubeMapWidth = 256;
        private int skyBoxCubeMapHeight = 256;
        private int mipCount = 9;

        // Copy shader vars
        private readonly int kSLPropMainTex_copy;
        private readonly int kSLPropLevel_copy;
        private readonly int kSLPropCurrentCubeFace_copy;
        // Blur shader vars
        private readonly int kSLPropMainTex_blur;
        private readonly int kSLPropTexel_blur;
        private readonly int kSLPropLevel_blur;
        private readonly int kSLPropScale_blur;
        private readonly int kSLPropCurrentCubeFace_blur;
        

        public SkyboxToCubemapRenderPass(Material material, Material cubeCopyMaterial, Material cubeBlurMaterial)
        {
            this.material = material;
            this.cubeCopyMaterial = cubeCopyMaterial;
            this.cubeBlurMaterial = cubeBlurMaterial;
            
            // Copy shader vars
            int tempPropId = cubeCopyMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropMainTex_copy = cubeCopyMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeCopyMaterial.shader.FindPropertyIndex("_MipLevel");
            kSLPropLevel_copy = cubeCopyMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeCopyMaterial.shader.FindPropertyIndex("_Current_CubeFace");
            kSLPropCurrentCubeFace_copy = cubeCopyMaterial.shader.GetPropertyNameId(tempPropId);
            // Blur shader vars
            tempPropId = cubeBlurMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropMainTex_blur = cubeBlurMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeBlurMaterial.shader.FindPropertyIndex("_TexelSize");
            kSLPropTexel_blur = cubeBlurMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeBlurMaterial.shader.FindPropertyIndex("_MipLevel");
            kSLPropLevel_blur = cubeBlurMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeBlurMaterial.shader.FindPropertyIndex("_BlurScale");
            kSLPropScale_blur = cubeBlurMaterial.shader.GetPropertyNameId(tempPropId);
            tempPropId = cubeBlurMaterial.shader.FindPropertyIndex("_Current_CubeFace");
            kSLPropCurrentCubeFace_blur = cubeBlurMaterial.shader.GetPropertyNameId(tempPropId);

            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
            profilingSampler = new ProfilingSampler($"{nameof(SkyboxToCubemapRendererFeature)}.{nameof(SkyboxToCubemapRenderPass)}");
            profilingSampler_convolution = new ProfilingSampler("SkyboxToCubemapRenderPass_convolution");
            profilingSampler_remapping = new ProfilingSampler("SkyboxToCubemapRenderPass_remapping");

            // It looks like upon Rendering Unity compensates UV Flip automatically
            materialPropertyBlock.SetFloat(flipCompensatedPropId, 1);
        }
        
        internal void SetSkyboxMaterial(Material skyboxMaterial)
        {
            this.skyboxMaterial = skyboxMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            const string initialPassName = "SkyboxToCubemap_InitialRender";
            const string convolutionPassName = "SkyboxToCubemap_Convolution";
            const string remappingPassName = "SkyboxToCubemap_Remapping";

            if (!material || !skyboxMaterial || !cubeCopyMaterial || !cubeBlurMaterial)
                return;

            // Copy properties from the original material as it is being constantly modified
            material.CopyPropertiesFromMaterial(skyboxMaterial);
            
            // PASS 1: Initial Skybox Rendering to Cubemap faces
            using (var builder = renderGraph.AddUnsafePass<CubeMapGenerationPassData>(initialPassName, out var passData))
            {
                passData.materialPropertyBlock = materialPropertyBlock;
                passData.newMaterial = material;
                passData.skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
                
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((CubeMapGenerationPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // Draw 6 faces of the cube map
                    for (var index = 0; index < FACES.Count; index++)
                    {
                        CubemapFace face = FACES[index];
                        context.cmd.SetRenderTarget(data.skyBoxCubeMapRTHandle, 0, face);
                        data.materialPropertyBlock.SetFloat(cubeMapFacePropId, (int)face);

                        // Draw fullscreen triangle
                        CoreUtils.DrawFullScreen(cmd, data.newMaterial, data.materialPropertyBlock);
                    }

                    cmd.GenerateMips(data.skyBoxCubeMapRTHandle);
                });
            }

            // PASS 2: Convolution pass
            using (var builder = renderGraph.AddUnsafePass<ConvolutionPassData>(convolutionPassName, out var passData))
            {
                passData.materialBlock = materialPropertyBlock;
                passData.sourceRTHandle = skyBoxCubeMapRTHandle;
                passData.scratchRTHandle = skyBoxCubeMapRTHandle_Scratch;
                passData.blurMaterial = cubeBlurMaterial;
                passData.texelPropId = kSLPropTexel_blur;
                passData.levelPropId = kSLPropLevel_blur;
                passData.scalePropId = kSLPropScale_blur;
                passData.facePropId = kSLPropCurrentCubeFace_blur;
                passData.mipCount = mipCount;
                passData.cubeMapWidth = skyBoxCubeMapWidth;

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((ConvolutionPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    for (var index = 0; index < FACES.Count; index++)
                    {
                        cmd.CopyTexture(
                            data.sourceRTHandle, index, 0,
                            data.scratchRTHandle, index, 0);
                    }

                    data.materialBlock.SetFloat(data.scalePropId, 1.0f);

                    int size = data.cubeMapWidth >> 1;
                    float texelSize = 1.0f / size; // should be 2.f/size, but size is already divided by two

                    for (int mipIndex = 1; mipIndex < data.mipCount; ++mipIndex)
                    {
                        RTHandle sourceRT, destRT;
                        bool isOddMip = (mipIndex & 1) == 1;
            
                        // Progressive blur, each mip level is blurred from the previous one
                        // This creates the accumulation effect seen in Unity's implementation
                        if (isOddMip)
                        {
                            // Odd mips - read from main, write to scratch
                            sourceRT = data.sourceRTHandle;
                            destRT = data.scratchRTHandle;
                        }
                        else
                        {
                            // Even mips - read from scratch, write to main
                            sourceRT = data.scratchRTHandle;
                            destRT = data.sourceRTHandle;
                        }

                        for (int nFaceIndex = 0; nFaceIndex < FACES.Count; ++nFaceIndex)
                        {
                            CubemapFace face = FACES[nFaceIndex];
                            
                            context.cmd.SetRenderTarget(destRT, mipIndex, face);
                            cmd.SetGlobalTexture(kSLPropMainTex_blur, sourceRT);

                            // Blur parameters
                            data.materialBlock.SetFloat(data.texelPropId, texelSize);
                            float level = mipIndex - 1.0f;
                            data.materialBlock.SetFloat(data.levelPropId, level);
                            data.materialBlock.SetFloat(data.facePropId, nFaceIndex);

                            CoreUtils.DrawFullScreen(cmd, data.blurMaterial, data.materialBlock);
                        }

                        size >>= 1;
                        texelSize *= 2.0f;
                    }
                    
                    // If the final blurred mips ended up in scratch, copy them back to main
                    // Krzysztof: If my assumption is right, this will never be called with fixed 9 mips.
                    bool needsFinalCopy = ((data.mipCount - 1) & 1) == 1;
                    if (needsFinalCopy)
                    {
                        for (int mipIndex = 1; mipIndex < data.mipCount; mipIndex += 2)
                        {
                            for (int faceIndex = 0; faceIndex < FACES.Count; faceIndex++)
                            {
                                cmd.CopyTexture(
                                    data.scratchRTHandle, faceIndex, mipIndex,
                                    data.sourceRTHandle, faceIndex, mipIndex);
                            }
                        }
                    }
                });
            }

            // PASS 3: Remapping pass (roughness-based sampling)
            using (var builder = renderGraph.AddUnsafePass<RemappingPassData>(remappingPassName, out var passData))
            {
                passData.materialBlock = materialPropertyBlock;
                passData.sourceRTHandle = skyBoxCubeMapRTHandle;
                passData.scratchRTHandle = skyBoxCubeMapRTHandle_Scratch;
                passData.copyMaterial = cubeCopyMaterial;
                passData.mipCount = mipCount;
                passData.cubeMapWidth = skyBoxCubeMapWidth;
                
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((RemappingPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    //MaterialPropertyBlock tempMatPropBlock = new MaterialPropertyBlock();
                    
                    // Coping all the blurred mips to scratch
                    for (int mipIndex = 0; mipIndex < data.mipCount; mipIndex++)
                    {
                        for (int faceIndex = 0; faceIndex < FACES.Count; faceIndex++)
                        {
                            cmd.CopyTexture(
                                data.sourceRTHandle, faceIndex, mipIndex,
                                data.scratchRTHandle, faceIndex, mipIndex);
                        }
                    }

                    cmd.SetGlobalTexture(kSLPropMainTex_copy, data.scratchRTHandle);

                    const int specularSteps = 7; // MM: using 7 instead of lod since this is what the baked probes use regardless of resolution (look for m_CubemapConvolutionSteps).
                    // TODO: This for 95% can be shortened, do sanity check on it
                    float step = 1.0f / (float)(specularSteps > 1 ? specularSteps - 1 : 1);
                    float roughness = step;
                    int size = data.cubeMapWidth >> 1;

                    for (int mipIndex = 1; mipIndex < data.mipCount; mipIndex++)
                    {
                        // MM: original power was 1.5. I changed it to make blur strengths similar to baked probes
                        float blurringRadius = Mathf.Pow(roughness, 1.9f) * (2.0f * data.cubeMapWidth);

                        int level = 7;
                        float lerpFactor = 0.0f;
                        
                        if (size > 1)
                        {
                            float n0;
                            
                            while ((n0 = WidthOf(level)) > blurringRadius) { --level; }
                            
                            float n1 = WidthOf(level + 1);
                            lerpFactor = (blurringRadius - n0) / (n1 - n0);
                        }
                        
                        for (int faceIndex = 0; faceIndex < FACES.Count; faceIndex++)
                        {
                            CubemapFace face = FACES[faceIndex];

                            context.cmd.SetRenderTarget(data.sourceRTHandle, mipIndex, face);

                            data.materialBlock.SetFloat(kSLPropLevel_copy, level + lerpFactor);
                            data.materialBlock.SetFloat(kSLPropCurrentCubeFace_copy, faceIndex);

                            CoreUtils.DrawFullScreen(cmd, data.copyMaterial, data.materialBlock);
                        }

                        roughness += step;
                        size >>= 1;
                    }
                });
            }
        }

        public int WidthOf(int level)
        {
            return 1 << (level + 1);
        }
        
        // Pass Data structures for each phase
        private class CubeMapGenerationPassData
        {
            internal MaterialPropertyBlock materialPropertyBlock;
            internal Material newMaterial;
            internal RTHandle skyBoxCubeMapRTHandle;
        }

        private class ConvolutionPassData
        {
            public MaterialPropertyBlock materialBlock;
            public RTHandle sourceRTHandle;
            public RTHandle scratchRTHandle;
            public Material blurMaterial;
            public int texelPropId;
            public int levelPropId;
            public int scalePropId;
            public int facePropId;
            public int mipCount;
            public int cubeMapWidth;
        }

        private class RemappingPassData
        {
            public MaterialPropertyBlock materialBlock;
            public RTHandle sourceRTHandle;
            public RTHandle scratchRTHandle;
            public Material copyMaterial;
            public int mipCount;
            public int cubeMapWidth;
        }
    }
