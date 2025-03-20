using DCL.Landscape.Settings;
using ECS;
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Utility;

namespace DCL.Rendering.GPUInstancing
{
    public partial class GPUInstancingRenderFeature : ScriptableRendererFeature
    {
        [SerializeField] private GPUInstancingRenderFeature_Settings m_Settings;

        public GPUInstancingRenderFeature_Settings Settings => m_Settings;

        private GPUInstancingService instancingService;
        private GPUInstancingRenderPass instancingRenderPass;

        public override void Create()
        {
            instancingRenderPass = new GPUInstancingRenderPass(instancingService)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingOpaques
            };
        }

        public void Initialize(GPUInstancingService service, IRealmData realmData)
        {
            instancingService = service;
            instancingRenderPass?.SetService(service, realmData);
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (instancingRenderPass != null)
                renderer.EnqueuePass(instancingRenderPass);
        }

        protected override void Dispose(bool _bDisposing)
        {
            // TODO: dispose renderPass if needed;
        }

        [Serializable]
        public class GPUInstancingRenderFeature_Settings
        {
            private const float SCALE_FACTOR = 0.3f;
            private const float SCENE_DIST_MIN = 20f;
            private const float ENV_DIST_MIN = 1000f;
            private const float ENV_DIST_MAX = 7000f;

            public ComputeShader FrustumCullingAndLODGenComputeShader;
            public ComputeShader IndirectBufferGenerationComputeShader;
            public ComputeShader DrawArgsInstanceCountTransferComputeShader;

            [SerializeField]
            private LandscapeData test;

            public float RoadsSceneDistance(float envDistance)
            {
                float t = (envDistance - ENV_DIST_MIN) / (ENV_DIST_MAX - ENV_DIST_MIN); // normalize to [0,1] range
                return SCENE_DIST_MIN + ((ParcelMathJobifiedHelper.RADIUS_HARD_LIMIT - SCENE_DIST_MIN) * t * SCALE_FACTOR);
            }
        }
    }
}
