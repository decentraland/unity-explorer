using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;
using DCL.Diagnostics;

namespace DCL.Rendering.RenderGraph.RenderFeatures.AvatarOutline
{
    public class RendererFeature_AvatarOutline : ScriptableRendererFeature
    {
        private readonly ReportData m_ReportData = new ("RendererFeature_AvatarOutline", ReportHint.SessionStatic);
        public static List<Renderer> m_AvatarOutlineRenderers = new ();
        private RenderPass_OutlineDraw m_OutlineDrawPass;

        public override void Create()
        {
            name = "RendererFeature_AvatarOutline";

            // Pass in constructor variables which don't/shouldn't need to be updated every frame.
            m_OutlineDrawPass = new RenderPass_OutlineDraw(m_AvatarOutlineRenderers);
            m_OutlineDrawPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;;
        }

        public override void OnCameraPreCull(ScriptableRenderer renderer, in CameraData cameraData)
        {

        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (m_OutlineDrawPass == null)
                return;

            renderer.EnqueuePass(m_OutlineDrawPass);
        }

        protected override void Dispose(bool disposing)
        {

        }
    }
}
