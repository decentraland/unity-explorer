using DCL.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// ReSharper disable InconsistentNaming

namespace DCL.SkyBox.Rendering
{
    public partial class DCL_RenderFeature_ProceduralSkyBox
    {
        public class DCL_RenderPass_DrawSkyBox : ScriptableRenderPass
        {
            private enum ShaderPasses
            {
                Scenery = 0,
                Stars = 1,
                Sky = 2,
            }

            //public static RTHandle k_CameraTarget
            private const string PROFILER_TAG = "Custom Pass: DrawSkyBox";

            private const string SKYBOX_CUBEMAP_TEXTURE_NAME = "_SkyBox_Cubemap_Texture";
            private const string STARBOX_CUBEMAP_TEXTURE_NAME = "_StarBox_Cubemap_Texture";
            private const string SPACE_CUBEMAP_TEXTURE_NAME = "_Space_Cubemap_Texture";
            private static readonly int s_SkyBoxCubemapTextureID = Shader.PropertyToID(SKYBOX_CUBEMAP_TEXTURE_NAME);
            private static readonly int s_StarBoxCubemapTextureID = Shader.PropertyToID(STARBOX_CUBEMAP_TEXTURE_NAME);
            private static readonly int s_SpaceCubemapTextureID = Shader.PropertyToID(SPACE_CUBEMAP_TEXTURE_NAME);
            private static readonly int s_SunPosID = Shader.PropertyToID("_SunPos");
            private readonly TimeOfDayRenderingModel timeOfDayRenderingModel;

            // Debug
            private readonly ReportData m_ReportData = new ("DCL_RenderPass_GenerateSkyBox", ReportDebounce.AssemblyStatic);
            private Material m_Material_Draw;
            private ProceduralSkyBoxSettings_Draw m_Settings_Draw;
            private RTHandle m_SkyBoxCubeMap_RTHandle;
            private RTHandle m_StarBoxCubeMap_RTHandle;
            private RTHandle m_cameraColorTarget_RTHandle;
            private RTHandle m_cameraDepthTarget_RTHandle;
            private Mesh m_cubeMesh;
            private Cubemap m_spaceCubemap;

            private bool bComputeStarMap = false;

            internal DCL_RenderPass_DrawSkyBox(TimeOfDayRenderingModel timeOfDayRenderingModel, bool _bComputeStarMap)
            {
                bComputeStarMap = _bComputeStarMap;
                bComputeStarMap = false;
                this.timeOfDayRenderingModel = timeOfDayRenderingModel;
                MakeCube();
            }

            internal void Setup(ProceduralSkyBoxSettings_Draw _featureSettings, Material _material, RTHandle _cameraColorTarget, RTHandle _cameraDepthTarget, RTHandle _SkyBox_Cubemap_Texture,
                RTHandle _StarBox_Cubemap_Texture, Cubemap _spaceCubemap)
            {
                bComputeStarMap = false;
                m_Material_Draw = _material;
                m_Settings_Draw = _featureSettings;
                m_cameraColorTarget_RTHandle = _cameraColorTarget;
                m_cameraDepthTarget_RTHandle = _cameraDepthTarget;
                m_SkyBoxCubeMap_RTHandle = _SkyBox_Cubemap_Texture;
                m_StarBoxCubeMap_RTHandle = _StarBox_Cubemap_Texture;
                m_spaceCubemap = _spaceCubemap;
                //m_spaceCubemap.dimension = TextureDimension.Cube;
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.

            public override void OnCameraSetup(CommandBuffer _cmd, ref RenderingData _renderingData)
            {
                var position = new Vector3(0.0f, 0.0f, -1.0f);
                Vector3 rotation = timeOfDayRenderingModel.GetSunPosLocal();
                var scale = new Vector3(1, 1, 1);

                var lightMat = Matrix4x4.TRS(position, Quaternion.Euler(rotation), scale);
                m_Material_Draw.SetMatrix(s_SunPosID, lightMat);
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                ConfigureClear(ClearFlag.None, Color.white);
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (m_Material_Draw == null)
                {
                    ReportHub.LogError(m_ReportData, $"{GetType().Name}.Execute(): Missing material. DCL_RenderPass_DrawSkyBox pass will not execute. Check for missing reference in the renderer resources.");
                    return;
                }

                CommandBuffer cmd = CommandBufferPool.Get();

                using (new ProfilingScope(cmd, new ProfilingSampler(PROFILER_TAG)))
                {
                    CoreUtils.SetRenderTarget(cmd, m_cameraColorTarget_RTHandle, m_cameraDepthTarget_RTHandle, clearFlag: ClearFlag.None, clearColor: Color.black, miplevel: 0, cubemapFace: CubemapFace.Unknown, depthSlice: -1);
                    cmd.SetGlobalTexture(s_SpaceCubemapTextureID, m_spaceCubemap);
                    if (bComputeStarMap == true)
                        cmd.SetGlobalTexture(s_StarBoxCubemapTextureID, m_StarBoxCubeMap_RTHandle);
                    cmd.SetGlobalTexture(s_SkyBoxCubemapTextureID, m_SkyBoxCubeMap_RTHandle);

                    cmd.DrawMesh(GetCube(), Matrix4x4.identity, m_Material_Draw, submeshIndex: 0, (int)ShaderPasses.Scenery, properties: null);
                    if (bComputeStarMap == true)
                        cmd.DrawMesh(GetCube(), Matrix4x4.identity, m_Material_Draw, submeshIndex: 0, (int)ShaderPasses.Stars, properties: null);
                    cmd.DrawMesh(GetCube(), Matrix4x4.identity, m_Material_Draw, submeshIndex: 0, (int)ShaderPasses.Sky, properties: null);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // Cleanup any allocated resources that were created during the execution of this render pass.
            public override void OnCameraCleanup(CommandBuffer cmd) { }

            public void Dispose()
            {
                m_SkyBoxCubeMap_RTHandle?.Release();
                m_cubeMesh = null;
            }

            private Vector3[] GetVertices()
            {
                float cubeLength = 100000;
                float cubeWidth = 100000;
                float cubeHeight = 100000;
                var vertice_0 = new Vector3(-cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
                var vertice_1 = new Vector3(cubeLength * .5f, -cubeWidth * .5f, cubeHeight * .5f);
                var vertice_2 = new Vector3(cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
                var vertice_3 = new Vector3(-cubeLength * .5f, -cubeWidth * .5f, -cubeHeight * .5f);
                var vertice_4 = new Vector3(-cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
                var vertice_5 = new Vector3(cubeLength * .5f, cubeWidth * .5f, cubeHeight * .5f);
                var vertice_6 = new Vector3(cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);
                var vertice_7 = new Vector3(-cubeLength * .5f, cubeWidth * .5f, -cubeHeight * .5f);

                Vector3[] vertices =
                {
                    // Bottom Polygon
                    vertice_0, vertice_1, vertice_2, vertice_3,

                    // Left Polygon
                    vertice_7, vertice_4, vertice_0, vertice_3,

                    // Front Polygon
                    vertice_4, vertice_5, vertice_1, vertice_0,

                    // Back Polygon
                    vertice_6, vertice_7, vertice_3, vertice_2,

                    // Right Polygon
                    vertice_5, vertice_6, vertice_2, vertice_1,

                    // Top Polygon
                    vertice_7, vertice_6, vertice_5, vertice_4,
                };

                return vertices;
            }

            private Vector3[] GetNormals()
            {
                Vector3 up = Vector3.up;
                Vector3 down = Vector3.down;
                Vector3 front = Vector3.forward;
                Vector3 back = Vector3.back;
                Vector3 left = Vector3.left;
                Vector3 right = Vector3.right;

                Vector3[] normales =
                {
                    // Bottom Side Render
                    down, down, down, down,

                    // LEFT Side Render
                    left, left, left, left,

                    // FRONT Side Render
                    front, front, front, front,

                    // BACK Side Render
                    back, back, back, back,

                    // RIGHT Side Render
                    right, right, right, right,

                    // UP Side Render
                    up, up, up, up,
                };

                return normales;
            }

            private Vector2[] GetUVsMap()
            {
                var _00_CORDINATES = new Vector2(0f, 0f);
                var _10_CORDINATES = new Vector2(1f, 0f);
                var _01_CORDINATES = new Vector2(0f, 1f);
                var _11_CORDINATES = new Vector2(1f, 1f);

                Vector2[] uvs =
                {
                    // Bottom
                    _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,

                    // Left
                    _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,

                    // Front
                    _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,

                    // Back
                    _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,

                    // Right
                    _11_CORDINATES, _01_CORDINATES, _00_CORDINATES, _10_CORDINATES,

                    // Top
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

            private void MakeCube()
            {
                if (m_cubeMesh == null)
                {
                    m_cubeMesh = new Mesh();
                    m_cubeMesh.vertices = GetVertices();
                    m_cubeMesh.normals = GetNormals();
                    m_cubeMesh.uv = GetUVsMap();
                    m_cubeMesh.triangles = GetTriangles();
                    m_cubeMesh.RecalculateBounds();
                    m_cubeMesh.RecalculateNormals();
                    m_cubeMesh.Optimize();
                }
            }

            private Mesh GetCube()
            {
                MakeCube();
                return m_cubeMesh;
            }
        }
    }
}
