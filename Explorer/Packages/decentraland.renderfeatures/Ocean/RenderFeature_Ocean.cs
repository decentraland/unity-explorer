using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.Ocean
{
    public class RendererFeature_Ocean : ScriptableRendererFeature
    {
        public static RendererFeature_Ocean GetDefault()
        {
            return (RendererFeature_Ocean)PipelineUtilities.GetRenderFeature<RendererFeature_Ocean>();
        }

        [Serializable]
        public class ScreenSpaceReflectionSettings
        {
            public bool enable;
        }
        public ScreenSpaceReflectionSettings screenSpaceReflectionSettings = new ScreenSpaceReflectionSettings();

        [Tooltip("Project caustics from the main directional light.")]
        public bool directionalCaustics;

        public RenderPass_Displacement.Settings displacementPrePassSettings = new RenderPass_Displacement.Settings();

        private RenderPass_CalculateBuffers calcBuffersPass;
        private RenderPass_Displacement displacementPass;

        public override void Create()
        {
            calcBuffersPass = new RenderPass_CalculateBuffers
            {
                renderPassEvent = RenderPassEvent.BeforeRendering
            };

            displacementPass = new RenderPass_Displacement
            {
                renderPassEvent = RenderPassEvent.BeforeRendering
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            calcBuffersPass.Setup(this);
            renderer.EnqueuePass(calcBuffersPass);

            if (displacementPrePassSettings.enable)
            {
                displacementPass.Setup(displacementPrePassSettings);
                renderer.EnqueuePass(displacementPass);
            }
        }

        private void OnDestroy()
        {
            displacementPass.Dispose();
            calcBuffersPass.Dispose();
        }
    }
}
