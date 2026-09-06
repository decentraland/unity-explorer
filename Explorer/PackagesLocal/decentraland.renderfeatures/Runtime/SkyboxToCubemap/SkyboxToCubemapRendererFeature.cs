using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.SkyboxToCubemap
{
    /// <summary>
    ///     Captures the active skybox into an HDR cubemap each cadence tick and
    ///     publishes it as <see cref="RenderSettings.customReflectionTexture"/>, so
    ///     metallic/smooth surfaces reflect the current sky. The bake + prefilter mip
    ///     chain live in <see cref="SkyboxCubemapCore"/>; this feature just owns the
    ///     serialized settings the renderer asset binds and wires them in.
    /// </summary>
    public class SkyboxToCubemapRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            [SerializeField] public Shader skyBoxShader;

            // The renderer asset binds DCL/CubeBlur here; that reference is what
            // carries the shader into player builds, where nothing else references
            // it and Shader.Find would come back empty.
            [SerializeField] public Shader cubeBlurShader;
            [SerializeField] public Material originalMaterial;
            [SerializeField] public int dimensions = 256;
            [SerializeField] public bool executeInEditMode;
            [SerializeField] public bool assignAsReflectionProbe = true;
        }

        [SerializeField] private Settings settings = new ();

        private SkyboxCubemapCore core;

        public override void Create()
        {
            // Unity re-runs Create on every domain reload and on every inspector edit
            // of the renderer asset; release what the previous run owned first.
            core?.Dispose();

            name = "SkyboxToCubemapRendererFeature";
            core = new SkyboxCubemapCore("DCL.SkyboxToCubemap");
            core.Setup(settings.skyBoxShader, settings.cubeBlurShader, settings.originalMaterial, settings.dimensions, settings.assignAsReflectionProbe);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (core == null || !core.IsReady)
                return;

            CameraType camType = renderingData.cameraData.cameraType;
            if (camType == CameraType.Reflection || camType == CameraType.Preview)
                return;
            if (camType == CameraType.SceneView && !settings.executeInEditMode)
                return;

            renderer.EnqueuePass(core.Pass);
        }

        /// <summary>Force a re-bake next frame (time-of-day / weather changes).</summary>
        public void MarkDirty() => core?.MarkDirty();

        protected override void Dispose(bool disposing)
        {
            core?.Dispose();
            core = null;
        }
    }
}
