using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;
#if URP
using UnityEngine.Rendering.Universal;
#if UNITY_2023_1_OR_NEWER
using UnityEngine.Rendering.RendererUtils;
#endif

namespace StylizedGrass
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "StylizedGrass", "sc.stylizedgrass.runtime", "GrassBendingFeature")]
    public class GrassRenderFeature : ScriptableRendererFeature
    {
        public class RenderBendVectors : ScriptableRenderPass
        {
            private const string profilerTag = "Render Grass Bending Vectors";
            private static ProfilingSampler profilerSampler = new ProfilingSampler(profilerTag);
            private const string profilerTagPass = "Geometry to vectors";
            private static ProfilingSampler profilerSamplerRendering = new ProfilingSampler(profilerTagPass);

            private readonly Settings settings;
            
            public const int TexelsPerMeter = 8;
            private const float FRUSTUM_MULTIPLIER = 2f;

            //Rather than culling based on layers, only render shaders with this pass tag
            private const string LightModeTag = "GrassBender";
            
            private RTHandle renderTarget;
            
            private static readonly int vectorMapID = Shader.PropertyToID("_GrassOffsetVectors");
            private static readonly int vectorUVID = Shader.PropertyToID("_GrassBendCoords");

            private static Vector4 rendererCoords;

            private static Matrix4x4 projection { set; get; }
            private static  Matrix4x4 view { set; get; }
        
            private static Vector3 centerPosition;
            private static int resolution;
            public static int CurrentResolution;
            private static float orthoSize;
            private static Bounds bounds;

            private static readonly Quaternion viewRotation = Quaternion.Euler(new Vector3(-90f, 0f, 0f));
            private static readonly Vector3 viewScale = new Vector3(1, 1, -1);
            private static readonly Color neutralVector = new Color(0.5f, 0f, 0.5f, 0f);
            private static Rect viewportRect;
        
            //Render pass
            FilteringSettings m_FilteringSettings;
            RenderStateBlock m_RenderStateBlock;
            private readonly List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>()
            {
                new ShaderTagId(LightModeTag)
            };
            private static readonly Plane[] frustrumPlanes = new Plane[6];
            
            #if UNITY_2023_1_OR_NEWER
            private RendererListParams rendererListParams;
            private RendererList rendererList;
            #endif
            
            public RenderBendVectors(ref Settings settings)
            {
                this.settings = settings;
                m_FilteringSettings = new FilteringSettings(RenderQueueRange.all, -1);
                m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            }

            private static int CalculateResolution(float size)
            {
                int res = Mathf.RoundToInt(size * TexelsPerMeter);
                res = Mathf.NextPowerOfTwo(res);
                res = Mathf.Clamp(res, 256, 4096);
            
                return res;
            }

            private void SetupProjection(CommandBuffer cmd, Camera camera)
            {
                centerPosition = camera.transform.position + (camera.transform.forward * orthoSize);
                
                centerPosition = StabilizeProjection(centerPosition, (orthoSize * 2f) / resolution);
                bounds = new Bounds(centerPosition, Vector3.one * orthoSize);
                
                centerPosition -= (Vector3.up * orthoSize * FRUSTUM_MULTIPLIER);
                
                projection = Matrix4x4.Ortho(-orthoSize, orthoSize, -orthoSize, orthoSize, 0.03f, orthoSize * FRUSTUM_MULTIPLIER * 2f);
                
                view = Matrix4x4.TRS(centerPosition, viewRotation, viewScale).inverse;

                cmd.SetViewProjectionMatrices(view, projection);
                //RenderingUtils.SetViewAndProjectionMatrices(cmd, view, projection, false);

                viewportRect.width = resolution;
                viewportRect.height = resolution;
                cmd.SetViewport(new Rect(0,0, resolution, resolution));
                
                GeometryUtility.CalculateFrustumPlanes(projection * view, frustrumPlanes);
                
                //Position/scale of projection. Converted to a UV in the shader
                rendererCoords.x = 1f - bounds.center.x - 1f + orthoSize;
                rendererCoords.y = 1f - bounds.center.z - 1f + orthoSize;
                rendererCoords.z = orthoSize * 2f;
                rendererCoords.w = 1f; //Enable in shader
            
                cmd.SetGlobalVector(vectorUVID, rendererCoords);
            }
            
            //Important to snap the projection to the nearest texel. Otherwise pixel swimming is introduced when moving, due to bilinear filtering
            private static Vector3 StabilizeProjection(Vector3 pos, float texelSize)
            {
                float Snap(float coord, float cellSize) => Mathf.FloorToInt(coord / cellSize) * (cellSize) + (cellSize * 0.5f);

                return new Vector3(Snap(pos.x, texelSize), Snap(pos.y, texelSize), Snap(pos.z, texelSize));
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                orthoSize = Mathf.Max(5, settings.bendingRenderRange) * 0.5f;
                resolution = CalculateResolution(orthoSize);

                if (resolution != CurrentResolution || renderTarget == null)
                {
                    RTHandles.Release(renderTarget);
                    
                    renderTarget = RTHandles.Alloc(resolution, resolution, 1, DepthBits.None,
                        UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                        filterMode: FilterMode.Bilinear,
                        wrapMode: TextureWrapMode.Clamp,
                        name: "GrassOffsetVectors");
                }
                CurrentResolution = resolution;

                cmd.SetGlobalTexture(vectorMapID, renderTarget);
                
                ConfigureTarget(renderTarget);
                ConfigureClear(ClearFlag.Color, neutralVector);
            }
        
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();

                DrawingSettings drawingSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, SortingCriteria.RenderQueue | SortingCriteria.SortingLayer | SortingCriteria.QuantizedFrontToBack);
                drawingSettings.enableInstancing = !UniversalRenderPipeline.asset.useSRPBatcher;
                //drawingSettings.enableDynamicBatching = true; //Overrides SRP Batcher
                drawingSettings.perObjectData = PerObjectData.None;
                
                using (new ProfilingScope(cmd, profilerSampler))
                {
                    ref CameraData cameraData = ref renderingData.cameraData;

                    SetupProjection(cmd, cameraData.camera);
                    
                    //Execute current commands first
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    
                    using (new ProfilingScope(cmd, profilerSamplerRendering))
                    {
                        #if UNITY_2023_1_OR_NEWER
                        rendererListParams.cullingResults = renderingData.cullResults;
                        rendererListParams.drawSettings = drawingSettings;
                        rendererListParams.filteringSettings = m_FilteringSettings;
                        rendererList = context.CreateRendererList(ref rendererListParams);
                        
                        cmd.DrawRendererList(rendererList);
                        #else
                        context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref m_FilteringSettings, ref m_RenderStateBlock);
                        #endif
                    }

                    //Restore
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);
                }
            
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                FrameCleanup(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.SetGlobalVector(vectorUVID, Vector4.zero);
            }

            public void Dispose()
            {
                Shader.SetGlobalVector(vectorUVID, Vector4.zero);
                RTHandles.Release(renderTarget);
            }
            
            //Using data only from the matrices, to ensure what you're seeing closely represents them
            public static void DrawOrthographicViewGizmo()
            {
                Gizmos.matrix = Matrix4x4.identity;

                float near = frustrumPlanes[4].distance;
                float far = frustrumPlanes[5].distance;
                float height = near + far;

                Vector3 position = new Vector3(view.inverse.m03, view.inverse.m13 + (height * 0.5f), view.inverse.m23);
                Vector3 scale = new Vector3((frustrumPlanes[0].distance + frustrumPlanes[1].distance), height, frustrumPlanes[2].distance + frustrumPlanes[3].distance);

                //Gizmos.DrawSphere(new Vector3(view.inverse.m03, view.inverse.m13 + height, view.inverse.m23), 1f);
                Gizmos.DrawWireCube(position, scale);
                Gizmos.color = Color.white * 0.25f;
                Gizmos.DrawCube(position, scale);
            }
        }

        private class SetupConstants : ScriptableRenderPass
        {
            private readonly Settings settings;
                        
            private static readonly int _CameraForwardVector = Shader.PropertyToID("_CameraForwardVector");
            private static Vector4 cameraForwardVector;
            
            private readonly int _DitheringScaleOffset = Shader.PropertyToID("_DitheringScaleOffset");
            private static Vector4 ditheringScaleOffset;
            
            private readonly int _DitheringNoise = Shader.PropertyToID("_DitheringNoise");
            
            public SetupConstants(ref Settings settings)
            {
                this.settings = settings;
            }
            
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (settings.forwardPerspectiveCorrection)
                {
                    //Pass the camera's forward vector for perspective correction
                    //This must be explicit, since during the shadow casting pass, the projection is that of the light (not the camera)
                    cameraForwardVector = renderingData.cameraData.camera.transform.forward;
                    cameraForwardVector.w = 1f;
                }
                else
                {
                    cameraForwardVector.w = 0f;
                }
                    
                cmd.SetGlobalVector(_CameraForwardVector, cameraForwardVector);

                if (settings.ditheringNoise)
                {
                    cmd.SetGlobalTexture(_DitheringNoise, settings.ditheringNoise);

                    ditheringScaleOffset.x = 1f / settings.ditheringNoise.width;
                    ditheringScaleOffset.y = 1f / settings.ditheringNoise.height;

                    #if UNITY_2022_3_OR_NEWER
                    if (settings.animateDithering && renderingData.cameraData.antialiasing == AntialiasingMode.TemporalAntiAliasing)
                    {
                        //Jitter the UV coordinates to perform stochastic sampling of the dithering pattern
                        ditheringScaleOffset.z = (Random.value * 2f - 1f) * ditheringScaleOffset.x;
                        ditheringScaleOffset.w = (Random.value * 2f - 1f) * ditheringScaleOffset.y;
                    }
                    #endif

                    Shader.SetGlobalVector(_DitheringScaleOffset, ditheringScaleOffset);
                }
            }

            public override void OnCameraCleanup(CommandBuffer cmd)
            {
                cmd.SetGlobalVector(_CameraForwardVector, Vector4.zero);
                cmd.SetGlobalVector(_DitheringScaleOffset, Vector4.zero);
            }
        }
        
        [Serializable]
        public class Settings
        {
            public bool enableBending = true;
            [UnityEngine.Serialization.FormerlySerializedAs("renderRange")]
            [Min(10f)]
            public float bendingRenderRange = 50f;
            
            [Space]
            
            [Tooltip("Use the camera's forward direction for the perspective correction feature." +
                     "\n\nIf disabled, grass is bent away from the camera's position instead")]
            public bool forwardPerspectiveCorrection = true;
            
            [Tooltip("Texture used for the Distance/Angle fading shading feature.")]
            public Texture2D ditheringNoise;

            [Tooltip("For the Distance/Angle fading feature, animate the dithering pattern when Temporal Anti-Aliasing (TAA) is enabled. This tends to cause TAA to smooth out the noise, emulating true transparency." +
                     "\n\nHas no effect in the scene view.")]
            public bool animateDithering = true;

            [Space]
            
            [Tooltip("Do not execute this render feature for the scene-view camera. Helps to inspect the world while everything is rendering from the main camera's perspective")]
            public bool ignoreSceneView;
            [Tooltip("Do not execute this render feature for overlay camera's. Doing so has no practical effect (but does incur a minor performance hit), unless grass is actually rendered on one.")]
            public bool ignoreOverlayCamera = true;
        }

        [SerializeField] [HideInInspector]
        //Reference it, so that it's included in a build
        private Shader bendingShader;

        public Settings settings = new Settings();
        
        private void Reset()
        {
            bendingShader = Shader.Find(GrassBender.BEND_SHADER_NAME);
            
            #if UNITY_EDITOR
            settings.ditheringNoise = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath("81200413a40918d4d8702e94db29911c"));
            #endif
        }
        
        private SetupConstants constantsSetupPass;
        private RenderBendVectors bendingVectorPass;
        
        public override void Create()
        {
            if (constantsSetupPass == null)
            {
                constantsSetupPass = new SetupConstants(ref settings)
                {
                    renderPassEvent = RenderPassEvent.BeforeRendering
                };
            }
            
            if (settings.enableBending && bendingVectorPass == null)
            {
                bendingVectorPass = new RenderBendVectors(ref settings)
                {
                    //Still needs to be executing here, unable to properly restore the view/projection matrix.
                    renderPassEvent = RenderPassEvent.BeforeRendering
                };
            }
        }

        private void OnDisable()
        {
            if (settings.enableBending && bendingVectorPass != null) bendingVectorPass.Dispose();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var currentCam = renderingData.cameraData.camera;
            
            //Skip for any special use camera's (except scene view camera)
            if (currentCam.cameraType != CameraType.SceneView && (currentCam.cameraType == CameraType.Reflection || currentCam.cameraType == CameraType.Preview || currentCam.hideFlags != HideFlags.None)) return;

            //Skip overlay cameras
            if (settings.ignoreOverlayCamera && renderingData.cameraData.renderType == CameraRenderType.Overlay) return;
            
            #if UNITY_EDITOR
            if (settings.ignoreSceneView && currentCam.cameraType == CameraType.SceneView) return;
            #endif

            renderer.EnqueuePass(constantsSetupPass);
            if(settings.enableBending) renderer.EnqueuePass(bendingVectorPass);
        }
    }
}
#endif