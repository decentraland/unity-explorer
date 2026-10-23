using DCL.BevyMigration.Bridge;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.BevyMigration.Composite
{
    /// <summary>
    ///     URP renderer feature that composites Bevy's completed frame over the main camera's skybox, before
    ///     transparents and UI (BevyMigration/docs/README.md § 8 "Topology"): the frame is premultiplied by coverage
    ///     and transparent where Bevy drew nothing, so Unity's sky stays visible until the skybox step. Inert while
    ///     <see cref="BevyComposite.Source" /> is null, so it can stay installed on the renderer asset.
    /// </summary>
    public class BevyCompositeFeature : ScriptableRendererFeature
    {
        private const string SHADER_NAME = "Hidden/DCL/BevyComposite";

        private BevyCompositePass? pass;
        private Material? material;

        public override void Create()
        {
            Shader shader = Shader.Find(SHADER_NAME);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new BevyCompositePass(material) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
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
            CoreUtils.Destroy(material);
            material = null;
        }

        private class BevyCompositePass : ScriptableRenderPass
        {
            private const string PASS_NAME = "BevyComposite";

            private readonly Material? material;

            private RTHandle? imported;
            private Texture? importedTexture;

            public BevyCompositePass(Material? material)
            {
                this.material = material;
            }

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
                    passData.Material = material;
                    passData.ScaleBias = BevyComposite.FlipVertically ? new Vector4(1f, -1f, 0f, 1f) : new Vector4(1f, 1f, 0f, 0f);
                    builder.UseTexture(sourceHandle);
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (BlitPassData data, RasterGraphContext context) =>
                    {
                        // Without the shader (missing from the build) the frame overwrites the sky, as before.
                        if (data.Material != null)
                            Blitter.BlitTexture(context.cmd, data.Source, data.ScaleBias, data.Material, 0);
                        else
                            Blitter.BlitTexture(context.cmd, data.Source, data.ScaleBias, 0f, false);
                    });
                }
            }

            private class BlitPassData
            {
                public RTHandle Source = null!;
                public Material? Material;
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
