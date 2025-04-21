using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    public class RenderPass_Displacement : ScriptableRenderPass
    {
        private const string profilerTag = "Water Displacement Prepass";
        private static readonly ProfilingSampler profilerSampler = new ProfilingSampler(profilerTag);

        public const string KEYWORD = "WATER_DISPLACEMENT_PASS";
        /// <summary>
        /// Using this as a value comparison in shader code to determine if not water is being hit
        /// </summary>
        public const float VOID_THRESHOLD = -1000f;
        private Color targetClearColor = new Color(VOID_THRESHOLD, 0, 0, 0);

        [Serializable]
        public class Settings
        {
            public bool enable;

            public float range = 500f;

            [Range(0.1f, 4f)]
            public float cellSize = 0.25f;
        }

        //Render pass
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>()
        {
            new ShaderTagId("DepthOnly")
        };

        class DisplacementPassData
        {
            internal TextureHandle WaterDisplacementRenderTarget;
            internal RendererListHandle rendererListHandle;
        }

        public RenderPass_Displacement()
        {
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.all, LayerMask.GetMask("Water"));
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        private static readonly Quaternion viewRotation = Quaternion.Euler(new Vector3(90f, 0f, 0f));
        private static readonly Vector3 viewScale = new Vector3(1, 1, -1);
        private static Rect viewportRect;

        private const string BufferName = "_WaterDisplacementBuffer";
        private static readonly int _WaterDisplacementBuffer = Shader.PropertyToID(BufferName);
        private const string CoordsName = "_WaterDisplacementCoords";
        private static readonly int _WaterDisplacementCoords = Shader.PropertyToID(CoordsName);

        private RTHandle renderTarget;
        private static Vector3 centerPosition;
        private static Vector4 rendererCoords;

        private static Matrix4x4 projection { set; get; }
        private static Matrix4x4 view { set; get; }

        private int resolution;
        private int m_resolution;
        private float orthoSize;
        private Settings settings;

        private RendererListParams rendererListParams;
        private RendererList rendererList;
        private RenderTextureDescriptor WaterDisplacementRTDescriptor_Colour;

        public void Setup(Settings settings)
        {
            this.settings = settings;

            resolution = Mathf.CeilToInt(settings.range / settings.cellSize);
            resolution = Mathf.Clamp(resolution, 16, 2048);
            orthoSize = 0.5f * settings.range;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // // Configure start
            // if (resolution != m_resolution || renderTarget == null)
            // {
            //     RTHandles.Release(renderTarget);
            //
            //     renderTarget = RTHandles.Alloc(resolution, resolution, 1, DepthBits.None,
            //         UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat,
            //         filterMode: FilterMode.Bilinear,
            //         wrapMode: TextureWrapMode.Clamp,
            //         useMipMap: false,
            //         name: BufferName);
            // }

            m_resolution = resolution;

            // configure end

            //Execute Start
            using (var builder = renderGraph.AddUnsafePass<DisplacementPassData>("DisplacementPass", out var passData))
            {
                UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                UniversalResourceData universalResourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData universalCameraData = frameData.Get<UniversalCameraData>();
                UniversalLightData universalLightData = frameData.Get<UniversalLightData>();

                DrawingSettings drawingSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, universalRenderingData, universalCameraData, universalLightData,
                    SortingCriteria.RenderQueue | SortingCriteria.SortingLayer | SortingCriteria.CommonTransparent);
                drawingSettings.perObjectData = PerObjectData.None;
                var param = new RendererListParams(universalRenderingData.cullResults, drawingSettings, m_FilteringSettings);
                passData.rendererListHandle = renderGraph.CreateRendererList(param);

                WaterDisplacementRTDescriptor_Colour.width = universalCameraData.cameraTargetDescriptor.width;
                WaterDisplacementRTDescriptor_Colour.height = universalCameraData.cameraTargetDescriptor.height;
                WaterDisplacementRTDescriptor_Colour.msaaSamples = 1;
                WaterDisplacementRTDescriptor_Colour.graphicsFormat = GraphicsFormat.R16_SFloat;
                WaterDisplacementRTDescriptor_Colour.sRGB = false;

                passData.WaterDisplacementRenderTarget = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    WaterDisplacementRTDescriptor_Colour,
                    "WaterDisplacementRenderTarget",
                    clear: true,
                    filterMode: FilterMode.Bilinear,
                    wrapMode: TextureWrapMode.Clamp);

                // ConfigureTarget(renderTarget);
                // ConfigureClear(ClearFlag.Color, targetClearColor);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((DisplacementPassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    cmd.SetGlobalTexture(_WaterDisplacementBuffer, passData.WaterDisplacementRenderTarget);
                    cmd.EnableShaderKeyword(KEYWORD);
                    context.cmd.SetRenderTarget(passData.WaterDisplacementRenderTarget);

                    // SetupProjection Start
                    centerPosition = universalCameraData.camera.transform.position;
                    centerPosition += universalCameraData.camera.transform.forward * settings.range * 0.5f;

                    float texelSize = (settings.range) / resolution;

                    //Important to snap the projection to the nearest texel. Otherwise pixel swimming is introduced when moving, due to bilinear filtering
                    float Snap(float coord, float cellSize) =>
                        Mathf.FloorToInt(coord / cellSize) * (cellSize) + (cellSize * 0.5f);

                    centerPosition.x = Snap(centerPosition.x, texelSize);
                    centerPosition.y = Snap(centerPosition.y, texelSize);
                    centerPosition.z = Snap(centerPosition.z, texelSize);

                    //var frustumHeight = 2.0f * renderRange * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad); //Still clips, plus doesn't support orthographc
                    var frustumHeight = settings.range;
                    centerPosition += (Vector3.up * frustumHeight * 0.5f);

                    projection = Matrix4x4.Ortho(-orthoSize, orthoSize, -orthoSize, orthoSize, 0.03f, frustumHeight * 2f);

                    view = Matrix4x4.TRS(centerPosition, viewRotation, viewScale).inverse;

                    cmd.SetViewProjectionMatrices(view, projection);

                    //RenderingUtils.SetViewAndProjectionMatrices(cmd, view, projection, true);

                    viewportRect.width = resolution;
                    viewportRect.height = resolution;
                    cmd.SetViewport(viewportRect);

                    cmd.SetGlobalMatrix("UNITY_MATRIX_V", view);

                    //Position/scale of projection. Converted to a UV in the shader
                    rendererCoords.x = centerPosition.x - orthoSize;
                    rendererCoords.y = centerPosition.z - orthoSize;
                    rendererCoords.z = settings.range;
                    rendererCoords.w = 1f; //Enable in shader

                    cmd.SetGlobalVector(_WaterDisplacementCoords, rendererCoords);

                    // SetupProjection End

                    //Execute current commands first
                    // context.ExecuteCommandBuffer(cmd);
                    // cmd.Clear();



                    cmd.DrawRendererList(data.rendererListHandle);
                });
            }
        }

        public void Dispose()
        {
            Shader.SetGlobalVector(_WaterDisplacementCoords, Vector4.zero);
            RTHandles.Release(renderTarget);
        }
    }
}
