using DCL.Roads.GPUInstancing;
using ECS;
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public partial class GPUInstancingRenderFeature : ScriptableRendererFeature
{
    [SerializeField] private GPUInstancingRenderFeature_Settings m_Settings;

    public GPUInstancingRenderFeature_Settings Settings => m_Settings;

    private GPUInstancingService instancingService;
    private GPUInstancingRenderPass instancingRenderPass;
    public GPUInstancingRenderFeature()
    {
        m_Settings = new GPUInstancingRenderFeature_Settings();
    }

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

    // public override void SetupRenderPasses(ScriptableRenderer _renderer, in RenderingData _renderingData) { }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (instancingRenderPass != null)
            renderer.EnqueuePass(instancingRenderPass);
    }

    protected override void Dispose(bool _bDisposing)
    {
        // instancingRenderPass?.Dispose();
    }

    [Serializable]
    public class GPUInstancingRenderFeature_Settings
    {
        public ComputeShader FrustumCullingAndLODGenComputeShader;
        public ComputeShader IndirectBufferGenerationComputeShader;
        // You could add custom settings here if needed (e.g., pass event, etc.)
    }
}
