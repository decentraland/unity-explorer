using DCL.BevyMigration.Bridge;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
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

                TextureHandle sourceHandle = renderGraph.ImportTexture(imported);
                Vector2 scale = BevyComposite.FlipVertically ? new Vector2(1f, -1f) : Vector2.one;
                Vector2 offset = BevyComposite.FlipVertically ? new Vector2(0f, 1f) : Vector2.zero;

                using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass(PASS_NAME + " event", out RenderEventPassData passData))
                {
                    passData.Function = BevyBridge.RenderEventFunction;
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (RenderEventPassData data, UnsafeGraphContext context) =>
                    {
                        if (data.Function != System.IntPtr.Zero)
                            context.cmd.IssuePluginEvent(data.Function, BridgeRenderEvents.BEFORE_COMPOSITE);
                    });
                }

                renderGraph.AddBlitPass(sourceHandle, resourceData.activeColorTexture, scale, offset, passName: PASS_NAME);
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
