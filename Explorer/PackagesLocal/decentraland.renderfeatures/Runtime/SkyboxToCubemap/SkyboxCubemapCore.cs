using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace DCL.Rendering.RenderGraphs.RenderFeatures.SkyboxToCubemap
{
    /// <summary>
    ///     Ensures a single skybox→cubemap bake owns the global reflection each frame.
    ///     A renderer asset can carry several instances of the skybox feature;
    ///     whichever one records first takes ownership and the rest yield, so the sky
    ///     is baked and assigned to <see cref="RenderSettings.customReflectionTexture"/>
    ///     exactly once.
    /// </summary>
    internal static class SkyboxReflectionCoordinator
    {
        private static object owner;

        // "Enter Play Mode Options" can skip the domain reload, which would carry a
        // dead owner from the previous session into the next one and stop every
        // feature from ever baking.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOwner() => owner = null;

        internal static bool TryBecomeOwner(object self)
        {
            owner ??= self;
            return ReferenceEquals(owner, self);
        }

        internal static void Relinquish(object self)
        {
            if (ReferenceEquals(owner, self))
                owner = null;
        }
    }

    /// <summary>
    ///     Bakes the active skybox into a persistent HDR cubemap and prefilters it
    ///     into a roughness mip chain, then publishes it as the global reflection
    ///     probe. Owned by <see cref="SkyboxToCubemapRendererFeature"/>, which binds the
    ///     serialized settings. Runs on a real GPU (incl. Linux/Vulkan) — no early-return.
    ///
    ///     Pipeline per bake (cadence-gated):
    ///       1. Draw the fullscreen skybox material into six faces of a scratch cube
    ///          and of the output cube (mip 0), selecting each face via _CubemapFace.
    ///       2. GenerateMips on the scratch cube (box downsample).
    ///       3. For each output mip ≥ 1, DCL/CubeBlur convolves the scratch cube
    ///          (sampled at the matching LOD, widening radius) into the output mip.
    ///       4. Assign the output cube to RenderSettings.customReflectionTexture.
    ///
    ///     The two cubemaps are allocated on the first recorded frame after this
    ///     instance wins the coordinator election, so instances that never bake cost
    ///     no VRAM. They are released once, in Dispose; every later frame re-imports
    ///     the same handles, so there is no per-frame allocation.
    /// </summary>
    internal sealed class SkyboxCubemapCore
    {
        private const string CUBEBLUR_SHADER = "DCL/CubeBlur";
        private const int FORCED_REBAKE_INTERVAL_FRAMES = 30;

        // Roughness the deepest mip stands for, in the same tangent-plane units the
        // CubeBlur kernel spreads by.
        private const float MAX_CONVOLUTION_RADIUS = 1.6f;

        private static readonly int s_CubemapFaceId = Shader.PropertyToID("_CubemapFace");
        private static readonly int s_SourceCubeId = Shader.PropertyToID("_SourceCube");
        private static readonly int s_SourceMipId = Shader.PropertyToID("_SourceMip");
        private static readonly int s_BlurRadiusId = Shader.PropertyToID("_BlurRadius");

        private readonly string profilerTag;

        private RenderTexture scratchCube;
        private RenderTexture outputCube;
        private RTHandle scratchHandle;
        private RTHandle outputHandle;

        private Material captureMaterial;
        private Material cubeBlurMaterial;
        private bool ownsCaptureMaterial;

        private MaterialPropertyBlock propertyBlock;
        private BakePass pass;
        private bool assignAsReflectionProbe;
        private int cubeDimensions;
        private DefaultReflectionMode? previousReflectionMode;

        private bool dirty = true;
        private int framesSinceLastBake;
        private int mipCount;

        internal SkyboxCubemapCore(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }

        internal bool IsReady => pass != null;

        internal ScriptableRenderPass Pass => pass;

        /// <summary>Force a re-bake next frame (call when the sky visibly changes).</summary>
        internal void MarkDirty() => dirty = true;

        internal void Setup(Shader skyBoxShader, Shader cubeBlurShader, Material originalMaterial, int dimensions, bool assignReflection)
        {
            Dispose();

            if (skyBoxShader == null)
            {
                Debug.LogWarning($"[{profilerTag}] skyBoxShader is null; skybox reflection disabled.");
                return;
            }

            Shader blurShader = cubeBlurShader != null ? cubeBlurShader : Shader.Find(CUBEBLUR_SHADER);
            if (blurShader == null)
            {
                Debug.LogWarning($"[{profilerTag}] shader '{CUBEBLUR_SHADER}' not found; skybox reflection disabled.");
                return;
            }

            assignAsReflectionProbe = assignReflection;

            // The capture material drives the authored skybox look. If the feature
            // provides the original material, copy matching properties across so
            // authored sky params (colours, clouds, sun) carry into the fullscreen
            // capture variant.
            if (originalMaterial != null && originalMaterial.shader == skyBoxShader)
            {
                captureMaterial = originalMaterial;
                ownsCaptureMaterial = false;
            }
            else
            {
                captureMaterial = CoreUtils.CreateEngineMaterial(skyBoxShader);
                ownsCaptureMaterial = true;
                if (originalMaterial != null)
                    captureMaterial.CopyPropertiesFromMaterial(originalMaterial);
            }

            cubeBlurMaterial = CoreUtils.CreateEngineMaterial(blurShader);
            cubeDimensions = Mathf.Max(16, dimensions);

            propertyBlock = new MaterialPropertyBlock();
            pass = new BakePass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingOpaques };
            dirty = true;
        }

        internal void Dispose()
        {
            SkyboxReflectionCoordinator.Relinquish(this);

            // Clearing the reflection during engine teardown can throw once
            // RenderSettings is gone; swallow that narrow case only.
            try
            {
                if (assignAsReflectionProbe && outputCube != null && RenderSettings.customReflectionTexture == outputCube)
                {
                    RenderSettings.customReflectionTexture = null;

                    if (previousReflectionMode.HasValue)
                        RenderSettings.defaultReflectionMode = previousReflectionMode.Value;
                }
            }
            catch (System.NullReferenceException) { /* engine already tearing down */ }

            previousReflectionMode = null;

            if (scratchHandle != null) { RTHandles.Release(scratchHandle); scratchHandle = null; }
            if (outputHandle != null) { RTHandles.Release(outputHandle); outputHandle = null; }
            if (scratchCube != null) { scratchCube.Release(); CoreUtils.Destroy(scratchCube); scratchCube = null; }
            if (outputCube != null) { outputCube.Release(); CoreUtils.Destroy(outputCube); outputCube = null; }

            if (ownsCaptureMaterial) CoreUtils.Destroy(captureMaterial);
            ownsCaptureMaterial = false;
            captureMaterial = null;
            CoreUtils.Destroy(cubeBlurMaterial);
            cubeBlurMaterial = null;
            propertyBlock = null;
            pass = null;
        }

        // Deferred until this instance actually bakes: a renderer asset can hold
        // several skybox features and only the elected one ever needs the cubes.
        private void EnsureTargets()
        {
            if (outputCube != null)
                return;

            scratchCube = CreateCube(cubeDimensions, $"{profilerTag}_Scratch");
            outputCube = CreateCube(cubeDimensions, $"{profilerTag}_Reflection");
            mipCount = outputCube.mipmapCount;

            scratchHandle = RTHandles.Alloc(scratchCube);
            outputHandle = RTHandles.Alloc(outputCube);
        }

        private static RenderTexture CreateCube(int dim, string name)
        {
            var rt = new RenderTexture(dim, dim, 0, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear)
            {
                name = name,
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = false,
                antiAliasing = 1,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        // The time-of-day controller mutates the material bound to
        // RenderSettings.skybox (and in the editor that is a clone of the authored
        // asset), so an owned capture material has to re-read it before every bake or
        // the reflection stays frozen at the shader defaults.
        private void SyncCaptureFromActiveSky()
        {
            if (!ownsCaptureMaterial || captureMaterial == null)
                return;

            Material sky = RenderSettings.skybox;
            if (sky == null || sky == captureMaterial || sky.shader != captureMaterial.shader)
                return;

            captureMaterial.CopyPropertiesFromMaterial(sky);
        }

        // Publish the baked cube as the global custom reflection. Setting the
        // texture is not enough on its own — the default reflection mode must be
        // Custom for URP to sample it as the environment probe (unity_SpecCube0).
        private void AssignReflection()
        {
            if (!assignAsReflectionProbe)
                return;

            if (RenderSettings.defaultReflectionMode != DefaultReflectionMode.Custom)
            {
                previousReflectionMode ??= RenderSettings.defaultReflectionMode;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            }

            if (RenderSettings.customReflectionTexture != outputCube)
                RenderSettings.customReflectionTexture = outputCube;
        }

        private bool ConsumeDirty()
        {
            framesSinceLastBake++;
            if (dirty || framesSinceLastBake >= FORCED_REBAKE_INTERVAL_FRAMES)
            {
                dirty = false;
                framesSinceLastBake = 0;
                return true;
            }
            return false;
        }

        private sealed class PassData
        {
            internal Material CaptureMaterial;
            internal Material BlurMaterial;
            internal MaterialPropertyBlock Mpb;
            internal RenderTexture ScratchRT;
            internal TextureHandle Scratch;
            internal TextureHandle Output;
            internal int MipCount;
        }

        private sealed class BakePass : ScriptableRenderPass
        {
            private readonly SkyboxCubemapCore core;
            private readonly ProfilingSampler sampler;

            internal BakePass(SkyboxCubemapCore core)
            {
                this.core = core;
                sampler = new ProfilingSampler(core.profilerTag);
                profilingSampler = sampler;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (core.captureMaterial == null || core.cubeBlurMaterial == null)
                    return;

                // Only the coordinator-elected owner bakes + owns the reflection slot.
                if (!SkyboxReflectionCoordinator.TryBecomeOwner(core))
                    return;

                core.EnsureTargets();

                if (!core.ConsumeDirty())
                {
                    core.AssignReflection();
                    return;
                }

                core.SyncCaptureFromActiveSky();

                TextureHandle scratch = renderGraph.ImportTexture(core.scratchHandle);
                TextureHandle output = renderGraph.ImportTexture(core.outputHandle);

                using (var builder = renderGraph.AddUnsafePass<PassData>(core.profilerTag, out PassData data, sampler))
                {
                    data.CaptureMaterial = core.captureMaterial;
                    data.BlurMaterial = core.cubeBlurMaterial;
                    data.Mpb = core.propertyBlock;
                    data.ScratchRT = core.scratchCube;
                    data.Scratch = scratch;
                    data.Output = output;
                    data.MipCount = core.mipCount;

                    builder.UseTexture(scratch, AccessFlags.ReadWrite);
                    builder.UseTexture(output, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc(static (PassData d, UnsafeGraphContext ctx) =>
                    {
                        UnsafeCommandBuffer cmd = ctx.cmd;

                        // 1. Bake the fullscreen skybox into mip 0 of both cubes.
                        for (int face = 0; face < 6; face++)
                        {
                            d.Mpb.SetFloat(s_CubemapFaceId, face);
                            cmd.SetRenderTarget(d.Scratch, 0, (CubemapFace)face);
                            cmd.DrawProcedural(Matrix4x4.identity, d.CaptureMaterial, 0, MeshTopology.Triangles, 3, 1, d.Mpb);

                            cmd.SetRenderTarget(d.Output, 0, (CubemapFace)face);
                            cmd.DrawProcedural(Matrix4x4.identity, d.CaptureMaterial, 0, MeshTopology.Triangles, 3, 1, d.Mpb);
                        }

                        // 2. Box mip chain on the scratch cube (source for the convolution).
                        cmd.GenerateMips(d.ScratchRT);

                        // 3. Roughness convolution: scratch(LOD m-1) -> output mip m.
                        //    The radius follows the perceptual-roughness mapping URP
                        //    samples the chain with — mip/(mipCount-1) — so the last
                        //    mip really reads as fully rough instead of barely blurred.
                        cmd.SetGlobalTexture(s_SourceCubeId, d.Scratch);
                        float mipDenominator = Mathf.Max(1, d.MipCount - 1);
                        for (int mip = 1; mip < d.MipCount; mip++)
                        {
                            float radius = MAX_CONVOLUTION_RADIUS * mip / mipDenominator;
                            for (int face = 0; face < 6; face++)
                            {
                                d.Mpb.SetFloat(s_CubemapFaceId, face);
                                d.Mpb.SetFloat(s_SourceMipId, mip - 1);
                                d.Mpb.SetFloat(s_BlurRadiusId, radius);
                                cmd.SetRenderTarget(d.Output, mip, (CubemapFace)face);
                                cmd.DrawProcedural(Matrix4x4.identity, d.BlurMaterial, 0, MeshTopology.Triangles, 3, 1, d.Mpb);
                            }
                        }
                    });
                }

                core.AssignReflection();
            }
        }
    }
}
