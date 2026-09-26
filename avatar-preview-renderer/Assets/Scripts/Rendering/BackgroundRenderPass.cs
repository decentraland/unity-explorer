using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Rendering
{
    public class BackgroundRenderPass : ScriptableRenderPass
    {
        private readonly Material _material;

        public BackgroundRenderPass(Material material)
        {
            _material = material;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();

            if (resourceData.isActiveTargetBackBuffer) return;

            var colorTarget = resourceData.activeColorTexture;

            if (!colorTarget.IsValid())
                return;

            var drawParams = new RenderGraphUtils.BlitMaterialParameters(
                TextureHandle.nullHandle,
                colorTarget,
                _material,
                0 // Shader pass index
            );

            renderGraph.AddBlitPass(drawParams, "GradientPass");
        }
    }
}