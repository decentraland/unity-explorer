using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.VolumetricLight
{
    public partial class VolumetricLightRendererFeature : ScriptableRendererFeature
    {
        private class VolumetricLightRenderPass : ScriptableRenderPass
        {
            public void Setup(VolumetricLightRendererFeature_Settings _Settings, Material _outlineMaterial, RTHandle _outlineRTHandle, RenderTextureDescriptor _outlineRTDescriptor, RTHandle _depthNormalsRTHandle)
            {

            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {

            }

            public override void Execute(ScriptableRenderContext _context, ref RenderingData _renderingData)
            {

            }

            public override void FrameCleanup(CommandBuffer cmd)
            {

            }

            public void Dispose()
            {

            }
        }
    }
}
