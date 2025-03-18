using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.Avatar
{

    public partial class OutlineRendererFeature : ScriptableRendererFeature
    {
        private readonly ReportData m_ReportData = new ("DCL_RenderFeature_Outline", ReportHint.SessionStatic);

        public static List<Renderer> m_OutlineRenderers = new ();

        private OutlineDrawPass outlineDrawPass;

        public OutlineRendererFeature()
        {

        }

        public override void Create()
        {
            outlineDrawPass = new OutlineDrawPass(m_OutlineRenderers);
            outlineDrawPass.renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public override void SetupRenderPasses(ScriptableRenderer _renderer, in RenderingData _renderingData)
        {

        }

        public override void AddRenderPasses(ScriptableRenderer _renderer, ref RenderingData _renderingData)
        {
            _renderer.EnqueuePass(outlineDrawPass);
       }

        protected override void Dispose(bool _bDisposing)
        {
            outlineDrawPass?.Dispose();
        }
    }
}
