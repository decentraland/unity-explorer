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
        private GPUInstancingComputePass instancingComputePass;
        private GPUInstancingRenderPass instancingRenderPass;

        public override void Create()
        {
            instancingComputePass = new GPUInstancingComputePass(instancingService, m_Settings.settings)
            {
                renderPassEvent = RenderPassEvent.BeforeRendering
            };

            instancingRenderPass = new GPUInstancingRenderPass(instancingService, m_Settings.settings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques
            };
        }

        public void Initialize(GPUInstancingService service, IRealmData realmData)
        {
            instancingService = service;
            instancingComputePass?.SetService(service, realmData);
            instancingRenderPass?.SetService(service, realmData);
        }


        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (instancingComputePass != null)
                renderer.EnqueuePass(instancingComputePass);
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
            [SerializeField] public GPUInstancingSettings settings;

            public ComputeShader FrustumCullingAndLODGenComputeShader => settings.FrustumCullingAndLODGenComputeShader;
            public ComputeShader IndirectBufferGenerationComputeShader => settings.IndirectBufferGenerationComputeShader;
            public ComputeShader DrawArgsInstanceCountTransferComputeShader => settings.DrawArgsInstanceCountTransferComputeShader;

            public float RenderDistScaleFactor { get => settings.RenderDistScaleFactor; set => settings.RenderDistScaleFactor = value;}

            public float RoadsSceneDistance(float envDistance) =>
                settings.RoadsSceneDistance(envDistance);
        }
    }
}
