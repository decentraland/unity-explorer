using DCL.BevyMigration.Bridge;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.BevyMigration.Composite
{
    /// <summary>
    ///     URP renderer feature that blits Bevy's completed frame as the opaque base of the main camera, after the
    ///     skybox and before transparents and UI (BevyMigration/docs/README.md § 8 "Topology"). Inert while
    ///     <see cref="BevyComposite.Source" /> is null, so it can stay installed on the renderer asset.
    /// </summary>
    public class BevyCompositeFeature : ScriptableRendererFeature
    {
        private BevyCompositePass? pass;

        public override void Create()
        {
            pass = new BevyCompositePass { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (pass == null || BevyComposite.Source == null) return;
            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
        }

        private class BevyCompositePass : ScriptableRenderPass
        {
            private const string PASS_NAME = "BevyComposite";

            private RTHandle? imported;
            private Texture? importedTexture;

            public void Dispose()
            {
                imported?.Release();
                imported = null;
                importedTexture = null;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Texture? source = BevyComposite.Source;
                if (source == null) return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (!resourceData.activeColorTexture.IsValid()) return;

                if (!ReferenceEquals(importedTexture, source))
                {
                    imported?.Release();
                    imported = RTHandles.Alloc(source);
                    importedTexture = source;
                }

                if (imported == null) return;

                // An external texture has no RenderGraph descriptor, so it is imported with explicit info and
                // sampled by hand instead of through the blit utility.
                var info = new RenderTargetInfo { width = source.width, height = source.height, volumeDepth = 1, msaaSamples = 1, format = GraphicsFormat.B8G8R8A8_SRGB };
                TextureHandle sourceHandle = renderGraph.ImportTexture(imported, info);

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(PASS_NAME + " event", out RenderEventPassData eventData))
                {
                    eventData.Function = BevyBridge.RenderEventFunction;
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (RenderEventPassData data, UnsafeGraphContext context) =>
                    {
                        if (data.Function != System.IntPtr.Zero)
                            context.cmd.IssuePluginEvent(data.Function, BridgeRenderEvents.BEFORE_COMPOSITE);
                    });
                }

                using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PASS_NAME, out BlitPassData passData))
                {
                    passData.Source = imported;
                    passData.ScaleBias = BevyComposite.FlipVertically ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
                    builder.UseTexture(sourceHandle);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (BlitPassData data, RasterGraphContext context) =>
                        Blitter.BlitTexture(context.cmd, data.Source, data.ScaleBias, 0f, false));
                }
            }

            private class BlitPassData
            {
                public RTHandle Source = null!;
                public Vector4 ScaleBias;
            }

            private class RenderEventPassData
            {
                public System.IntPtr Function;
            }
        }
    }

    /// <summary>Mirrors `abi::render_events`.</summary>
    public static class BridgeRenderEvents
    {
        public const int BEFORE_COMPOSITE = 1;
    }
}
