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
        private int skyBoxCubeMapWidth;
        private int skyBoxCubeMapHeight;
        //private float fZSTEP = (1.0f / (1 << 16));

        // Copy shader vars
        private readonly int kSLPropMainTex_copy;
        private readonly int kSLPropLevel_copy;
        // Blur shader vars
        private readonly int kSLPropMainTex_blur;
        private readonly int kSLPropTexel_blur;
        private readonly int kSLPropLevel_blur;
        private readonly int kSLPropScale_blur;
        private readonly int kSLPropRadius_blur;
        private readonly int kSLPropTexA_blur;
        private readonly int kSLPropTexB_blur;
        private readonly int kSLPropValue_blur;
        private readonly int kSLPropCurrentCubeFace_blur;

        private readonly Mesh m_quadMesh;

        public SkyboxToCubemapRenderPass(Material material, Material originalMaterial, Material cubeCopyMaterial, Material cubeBlurMaterial)
        {
            this.material = material;
            this.originalMaterial = originalMaterial;
            this.cubeCopyMaterial = cubeCopyMaterial;
            this.cubeBlurMaterial = cubeBlurMaterial;

            // Copy shader vars
            kSLPropMainTex_copy = cubeCopyMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropLevel_copy = cubeCopyMaterial.shader.FindPropertyIndex("_Level");
            // Blur shader vars
            kSLPropMainTex_blur = cubeBlurMaterial.shader.FindPropertyIndex("_MainTex");
            kSLPropTexel_blur = cubeBlurMaterial.shader.FindPropertyIndex("_TexelSize");
            kSLPropLevel_blur = cubeBlurMaterial.shader.FindPropertyIndex("_MipLevel");
            kSLPropScale_blur = cubeBlurMaterial.shader.FindPropertyIndex("_BlurScale");
            // kSLPropRadius_blur = cubeBlurMaterial.shader.FindPropertyIndex("_Radius");
            // kSLPropTexA_blur = cubeBlurMaterial.shader.FindPropertyIndex("_TexA");
            // kSLPropTexB_blur = cubeBlurMaterial.shader.FindPropertyIndex("_TexB");
            // kSLPropValue_blur = cubeBlurMaterial.shader.FindPropertyIndex("_value");
            kSLPropCurrentCubeFace_blur = cubeBlurMaterial.shader.FindPropertyIndex("_Current_CubeFace");

            renderPassEvent = RenderPassEvent.BeforeRenderingSkybox;
            profilingSampler = new ProfilingSampler($"{nameof(SkyboxToCubemapRendererFeature)}.{nameof(SkyboxToCubemapRenderPass)}");
            profilingSampler_convolution = new ProfilingSampler("SkyboxToCubemapRenderPass_convolution");

            // It looks like upon Rendering Unity compensates UV Flip automatically
            materialPropertyBlock.SetFloat(flipCompensatedPropId, 1);

            //m_quadMesh = new Mesh();
            //m_quadMesh.vertices = GetVertices();
            //m_quadMesh.uv = GetUVsMap();
            //m_quadMesh.triangles = GetTriangles();
        }

        internal void Setup(RTHandle skyBoxCubeMapRTHandle, RTHandle skyBoxCubeMapRTHandle_Scratch)
        {
            this.skyBoxCubeMapRTHandle = skyBoxCubeMapRTHandle;
            this.skyBoxCubeMapRTHandle_Scratch = skyBoxCubeMapRTHandle_Scratch;
        }

        // private Vector3[] GetVertices()
        // {
        //     var vertice_0 = new Vector3(-cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
        //     var vertice_1 = new Vector3(cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
        //     var vertice_2 = new Vector3(cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
        //     var vertice_3 = new Vector3(-cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
        //
        //     Vector3[] vertices =
        //     {
        //         vertice_0, vertice_1, vertice_2, vertice_3,
        //     };
        //     return vertices;
        // }

        private Vector2[] GetUVsMap()
        {
            // if (SystemInfo.graphicsUVStartsAtTop)
            // {
            //     // OpenGL
            //     static float gl_coord[6][5][3] =
            //     {
            //         // XPOS: { 1, -v, -u }
            //         { { 1.0f, 1.0f, 1.0f }, { 1.0f, 1.0f, -1.0f }, { 1.0f, -1.0f, -1.0f }, { 1.0f, -1.0f, 1.0f } },
            //         // XNEG: { -1, -v, u }
            //         { { -1.0f, 1.0f, -1.0f }, { -1.0f, 1.0f, 1.0f }, { -1.0f, -1.0f, 1.0f }, { -1.0f, -1.0f, -1.0f } },
            //         // YPOS: { +u, 1, +v }
            //         { { -1.0f, 1.0f, -1.0f }, { 1.0f, 1.0f, -1.0f }, { 1.0f, 1.0f, 1.0f }, { -1.0f, 1.0f, 1.0f } },
            //         // YNEG: { +u, -1, -v }
            //         { { -1.0f, -1.0f, 1.0f }, { 1.0f, -1.0f, 1.0f }, { 1.0f, -1.0f, -1.0f }, { -1.0f, -1.0f, -1.0f } },
            //         // ZPOS: { +u, -v, 1 }
            //         { { -1.0f, 1.0f, 1.0f }, { 1.0f, 1.0f, 1.0f }, { 1.0f, -1.0f, 1.0f }, { -1.0f, -1.0f, 1.0f } },
            //         // ZNEG: { -u, -v, -1 }
            //         { { 1.0f, 1.0f, -1.0f }, { -1.0f, 1.0f, -1.0f }, { -1.0f, -1.0f, -1.0f }, { 1.0f, -1.0f, -1.0f } }
            //     };
            //     //coord = &gl_coord;
            // }
            // else
            // {
            //     // DirectX
            //     static float dx_coord[6][5][3] =
            //     {
            //         // XPOS: { 1, -v, +u }
            //         { { 1.0f, 1.0f, 1.0f }, { 1.0f, 1.0f, -1.0f }, { 1.0f, -1.0f, -1.0f }, { 1.0f, -1.0f, 1.0f } },
            //         // XNEG: { -1, -v, -u }
            //         { { -1.0f, 1.0f, -1.0f }, { -1.0f, 1.0f, 1.0f }, { -1.0f, -1.0f, 1.0f }, { -1.0f, -1.0f, -1.0f } },
            //         // YPOS: { +u, 1, +v }
            //         { { -1.0f, 1.0f, -1.0f }, { 1.0f, 1.0f, -1.0f }, { 1.0f, 1.0f, 1.0f }, { -1.0f, 1.0f, 1.0f } },
            //         // YNEG: { +u, -1, -v }
            //         { { -1.0f, -1.0f, 1.0f }, { 1.0f, -1.0f, 1.0f }, { 1.0f, -1.0f, -1.0f }, { -1.0f, -1.0f, -1.0f } },
            //         // ZPOS: { +u, -v, 1 }
            //         { { -1.0f, 1.0f, 1.0f }, { 1.0f, 1.0f, 1.0f }, { 1.0f, -1.0f, 1.0f }, { -1.0f, -1.0f, 1.0f } },
            //         // ZNEG: { -u, -v, -1 }
            //         { { 1.0f, 1.0f, -1.0f }, { -1.0f, 1.0f, -1.0f }, { -1.0f, -1.0f, -1.0f }, { 1.0f, -1.0f, -1.0f } }
            //     };
            //     //coord = &dx_coord;
            // }

            var _00_CORDINATES = new Vector2(0f, 0f);
            var _10_CORDINATES = new Vector2(1f, 0f);
            var _01_CORDINATES = new Vector2(0f, 1f);
            var _11_CORDINATES = new Vector2(1f, 1f);

            Vector2[] uvs =
            {
                // Bottom
                _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,
            };

            return uvs;
        }

        private int[] GetTriangles()
        {
            int[] triangles =
            {
                // Cube Bottom Side Triangles
                3, 1, 0,
                3, 2, 1,

                // Cube Left Side Triangles
                3 + (4 * 1), 1 + (4 * 1), 0 + (4 * 1),
                3 + (4 * 1), 2 + (4 * 1), 1 + (4 * 1),

                // Cube Front Side Triangles
                3 + (4 * 2), 1 + (4 * 2), 0 + (4 * 2),
                3 + (4 * 2), 2 + (4 * 2), 1 + (4 * 2),

                // Cube Back Side Triangles
                3 + (4 * 3), 1 + (4 * 3), 0 + (4 * 3),
                3 + (4 * 3), 2 + (4 * 3), 1 + (4 * 3),

                // Cube Rigth Side Triangles
                3 + (4 * 4), 1 + (4 * 4), 0 + (4 * 4),
                3 + (4 * 4), 2 + (4 * 4), 1 + (4 * 4),

                // Cube Top Side Triangles
                3 + (4 * 5), 1 + (4 * 5), 0 + (4 * 5),
                3 + (4 * 5), 2 + (4 * 5), 1 + (4 * 5),
            };

            return triangles;
        }

        private void MakeFace(int face, float z)
        {
            if (m_quadMesh == null)
            {

            }

            // float coord[6][5][3];
            // Mesh quadMesh = new Mesh();
            // quadMesh.vertices = new Vector3[]
            //

            //
            // device.ImmediateBegin(kPrimitiveQuads, vertexInput);
            //
            // device.ImmediateTexCoordAll((*coord)[face][0][0], (*coord)[face][0][1], (*coord)[face][0][2]);
            // device.ImmediateVertex(0, 0, z);
            //
            // device.ImmediateTexCoordAll((*coord)[face][3][0], (*coord)[face][3][1], (*coord)[face][3][2]);
            // device.ImmediateVertex(0, 1, z);
            //
            // device.ImmediateTexCoordAll((*coord)[face][2][0], (*coord)[face][2][1], (*coord)[face][2][2]);
            // device.ImmediateVertex(1, 1, z);
            //
            // device.ImmediateTexCoordAll((*coord)[face][1][0], (*coord)[face][1][1], (*coord)[face][1][2]);
            // device.ImmediateVertex(1, 0, z);
        }

        public int WidthOf(int level)
        {
            return 1 << (level + 1);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (!material || !originalMaterial || !cubeCopyMaterial || !cubeBlurMaterial) return;

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

                Material tempMat = new Material(cubeBlurMaterial.shader);
                MaterialPropertyBlock tempMatPropBlock = new MaterialPropertyBlock();

                int mipCount = 9;
                int size = skyBoxCubeMapWidth >> 1;
                float texelSize = 1.0f / size; // should be 2.f/size, but size is already divided by two
                for (int mipIndex = 1; mipIndex < mipCount; ++mipIndex)
                {
                    for (int nFaceIndex = 0; nFaceIndex < FACES.Count; ++nFaceIndex)
                    {
                        CubemapFace face = FACES[nFaceIndex];
                        CoreUtils.SetRenderTarget(cmd, skyBoxCubeMapRTHandle_Scratch, ClearFlag.None, Color.green, mipIndex, face);

                        cmd.SetGlobalTexture("_MainTex", skyBoxCubeMapRTHandle);
                        cmd.SetGlobalFloat(kSLPropCurrentCubeFace_blur, nFaceIndex);
                        // tempMat.SetFloat(kSLPropTexel_blur, texelSize);
                        // // Output mip range -> normalized range -> input mip range
                        // float level = mipIndex - 1.0f;
                        // tempMat.SetFloat(kSLPropLevel_blur, level);
                        // tempMat.SetFloat(kSLPropScale_blur, 1.0f);
                        // tempMat.SetFloat(kSLPropCurrentCubeFace_blur, nFaceIndex);

                        CoreUtils.DrawFullScreen(cmd, tempMat);
                    }
                    texelSize *= 2;
                }

                const int specularSteps = 7;    // MM: using 7 instead of lod since this is what the baked probes use regardless of resolution (look for m_CubemapConvolutionSteps).
                float step = 1.0f / (float)(specularSteps > 1 ? specularSteps - 1 : 1);

                float roughness = step;
                int cubeMapWidth = 256;
                for (int mipIndex = 1; mipIndex <= mipCount; ++mipIndex)
                {
                    for (int nFaceIndex = 0; nFaceIndex < FACES.Count; ++nFaceIndex)
                    {
                        CubemapFace face = FACES[nFaceIndex];
                        // MM: original power was 1.5. I changed it to make blur strengths similar to baked probes
                        float width = Mathf.Pow(roughness, 1.9f) * (2 * cubeMapWidth);

                        int level;
                        float f;

                        if (size > 1)
                        {
                            level = 7;
                            float n0;

                            while ((n0 = WidthOf(level)) > width)
                            {
                                --level;
                            }

                            float n1 = WidthOf(level + 1);
                            f = (width - n0) / (n1 - n0);
                        }
                        else
                        {
                            level = 7;
                            f = 0f;
                        }

                        cubeCopyMaterial.SetTexture("_MainTex", skyBoxCubeMapRTHandle_Scratch);
                        cubeCopyMaterial.SetFloat("_Level", level + f);

                        CoreUtils.SetRenderTarget(cmd, skyBoxCubeMapRTHandle, ClearFlag.None, Color.green, mipIndex, face);
                        CoreUtils.DrawFullScreen(cmd, cubeCopyMaterial);

                        //CubemapBlit(inputCubemap, copyMaterail, 0, mipIndex, z);
                        roughness += step;
                        size >>= 1;

                        //z -= zStep;
                    }
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
