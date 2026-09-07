using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;
using UnityEngine.Rendering.RenderGraphModule;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    public class RenderPass_CalculateBuffers : ScriptableRenderPass
    {
        private static readonly int _EnableDirectionalCaustics = Shader.PropertyToID("_EnableDirectionalCaustics");
        private static readonly int CausticsProjection = Shader.PropertyToID("CausticsProjection");
        private static readonly int _WaterSSREnabled = Shader.PropertyToID("_WaterSSREnabled");
        private static readonly int _WaterDisplacementPrePassAvailable = Shader.PropertyToID("_WaterDisplacementPrePassAvailable");

        private bool m_directionalCaustics;

        private static VisibleLight mainLight;
        private Matrix4x4 causticsProjection;

        private ScriptableRenderPassInput requirements;
        private RendererFeature_Ocean settings;

        class CalcBufferPassData
        {

        }

        public RenderPass_CalculateBuffers()
        {
            //Force a unit scale, otherwise affects the projection tiling of the caustics
            causticsProjection = Matrix4x4.Scale(Vector3.one);
        }

        public void Setup(RendererFeature_Ocean renderFeature)
        {
            this.settings = renderFeature;
            m_directionalCaustics = settings.directionalCaustics;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Configure Start
            //Inform the render pipeline which pre-passes are required
            //requirements = ScriptableRenderPassInput.None;

            //Only when using advanced shading, so don't forcibly enable
            //if(m_directionalCaustics) requirements = ScriptableRenderPassInput.Depth;
            //
            // if (settings.screenSpaceReflectionSettings.enable)
            // {
            //     requirements |= ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth;
            // }



            //ConfigureInput(requirements);
            // Configure End

            // Execute Start
            using (var builder = renderGraph.AddUnsafePass<CalcBufferPassData>("CalcBufferPass", out var passData))
            {
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((CalcBufferPassData data, UnsafeGraphContext context) =>
                {
                    UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
                    UniversalResourceData universalResourceData = frameData.Get<UniversalResourceData>();
                    UniversalCameraData universalCameraData = frameData.Get<UniversalCameraData>();
                    UniversalLightData universalLightData = frameData.Get<UniversalLightData>();

                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    if(settings.displacementPrePassSettings.enable)
                        cmd.EnableShaderKeyword(RenderPass_Displacement.KEYWORD);
                    else
                        cmd.DisableShaderKeyword(RenderPass_Displacement.KEYWORD);

                    cmd.SetGlobalInt(_WaterSSREnabled, settings.screenSpaceReflectionSettings.enable ? 1 : 0);
                    cmd.SetGlobalInt(_WaterDisplacementPrePassAvailable, settings.displacementPrePassSettings.enable ? 1 : 0);

                    if (m_directionalCaustics)
                    {
                        //When no lights are visible, main light will be set to -1.
                        if (universalLightData.mainLightIndex > -1)
                        {
                            mainLight = universalLightData.visibleLights[universalLightData.mainLightIndex];

                            if (mainLight.lightType == LightType.Directional)
                            {
                                causticsProjection = Matrix4x4.Rotate(mainLight.light.transform.rotation);

                                cmd.SetGlobalMatrix(CausticsProjection, causticsProjection.inverse);
                            }

                            //Sets up the required View- -> Clip-space matrices
                            NormalReconstruction.SetupProperties(cmd, universalCameraData);
                        }
                        else
                        {
                            m_directionalCaustics = false;
                        }
                    }

                    cmd.SetGlobalInt(_EnableDirectionalCaustics, m_directionalCaustics ? 1 : 0);
                });

                // Execute End
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.SetGlobalInt(_EnableDirectionalCaustics, 0);
            cmd.SetGlobalInt(_WaterSSREnabled, 0);
        }

        public void Dispose()
        {
            Shader.SetGlobalInt(_WaterDisplacementPrePassAvailable, 0);
        }
    }
}
