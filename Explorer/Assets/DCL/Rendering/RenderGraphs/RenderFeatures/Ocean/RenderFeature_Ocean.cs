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

        private RenderPass_CalculateBuffers constantsSetup;
        private RenderPass_Displacement displacementPass;

        void OnEnable()
        {

        }

        public override void Create()
        {
            constantsSetup = new RenderPass_CalculateBuffers
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
            constantsSetup.Setup(this);
            renderer.EnqueuePass(constantsSetup);

            if (displacementPrePassSettings.enable)
            {
                displacementPass.Setup(displacementPrePassSettings);
                renderer.EnqueuePass(displacementPass);
            }
        }

        private void OnDestroy()
        {
            displacementPass.Dispose();
            constantsSetup.Dispose();
        }
    }
}
