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
            [SerializeField] private GPUInstancingSettings settings;

            public ComputeShader FrustumCullingAndLODGenComputeShader => settings.FrustumCullingAndLODGenComputeShader;
            public ComputeShader IndirectBufferGenerationComputeShader => settings.IndirectBufferGenerationComputeShader;
            public ComputeShader DrawArgsInstanceCountTransferComputeShader => settings.DrawArgsInstanceCountTransferComputeShader;

            public float RenderDistScaleFactor { get => settings.RenderDistScaleFactor; set => settings.RenderDistScaleFactor = value;}

            public float RoadsSceneDistance(float envDistance) =>
                settings.RoadsSceneDistance(envDistance);
        }
    }
}
