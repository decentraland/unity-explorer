// Hooks RenderPipelineManager.beginCameraRendering (SRP) and Camera.onPreCull
// (built-in pipeline) so TreeRendererService.RenderFrame runs once per frame
// for each camera that actually displays the instanced prototypes.
//
// Setup runs at AfterAssembliesLoaded so it's active for both EditMode and
// PlayMode tests (and runtime). Idempotent — safe to call multiple times.

using UnityEngine;
using UnityEngine.Rendering;

namespace GPUInstancerPro
{
    internal static class TreeRendererBootstrap
    {
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Install()
        {
            if (installed) return;
            installed = true;
            Camera.onPreCull -= OnLegacyPreCull;
            Camera.onPreCull += OnLegacyPreCull;
            RenderPipelineManager.beginCameraRendering -= OnSrpBeginCamera;
            RenderPipelineManager.beginCameraRendering += OnSrpBeginCamera;
        }

        private static void OnLegacyPreCull(Camera camera)
        {
            // Under an SRP, beginCameraRendering already fires for every
            // camera; letting this hook through as well would render each
            // frame twice into the same buffers.
            if (GraphicsSettings.currentRenderPipeline != null) return;
            if (!DrivesRendering(camera)) return;
            GPUICoreAPI.RenderFrameForCamera(camera);
        }

        private static void OnSrpBeginCamera(ScriptableRenderContext _, Camera camera)
        {
            if (!DrivesRendering(camera)) return;
            GPUICoreAPI.RenderFrameForCamera(camera);
        }

        // Every camera shares one set of per-LOD GraphicsBuffers and one stats
        // block, so only a camera that displays the registered prototypes may
        // drive a frame: a scene-view, preview or reflection-probe camera, or a
        // game camera whose culling mask excludes every drawn layer, would
        // overwrite the buffers the displayed draws still reference and reset
        // the stats that describe them.
        private static bool DrivesRendering(Camera camera) =>
            camera != null
            && camera.cameraType == CameraType.Game
            && (camera.cullingMask & GPUICoreAPI.RegisteredDrawLayerMask) != 0;
    }
}
