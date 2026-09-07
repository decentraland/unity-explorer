using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Utility;

namespace DCL.GoldenCapture
{
    using Time = UnityEngine.Time;

    /// <summary>
    ///     Golden-capture determinism harness (DCL_PLAZABENCH_DETERM_CLOCK=1). Driven entirely from a
    ///     static <see cref="RenderPipelineManager.beginContextRendering"/> handler so nothing the client
    ///     does to the scene graph can tear it down. Timeline: real-time wait for the world to load →
    ///     engage a fixed simulation step (and signal the scene-JS clock pin) for a short window →
    ///     normalize stochastic state (particles, animators, Random) → freeze (time stops, TAA off) and
    ///     re-issue the pinned shader time inside every camera's render passes so all _Time-driven
    ///     effects hold an identical phase across runs.
    /// </summary>
    public static class GoldenFreeze
    {
        private const float LOCKSTEP_DELTA = 1f / 60f;
        private const float FROZEN_SHADER_TIME = 100f;

        // Freeze only when game time crosses a multiple of this quantum, so every run freezes at
        // the same absolute Time.time (± one lockstep step) and all game-time-driven phases —
        // client-side tweens, Time.time consumers — land at an identical point across runs.
        private const double FREEZE_QUANTUM = 60.0;

        private static bool armed;
        private static bool lockstepEngaged;
        private static bool frozen;
        private static int frozenFrames;
        private static int lastTextureCount;
        private static int stableTextureSamples;
        private static bool pinLods;
        private static float startRealtime;
        private static double engagedAt;

        // Same process-start epoch as the scene-clock and fetch holds (SceneRuntimeImpl,
        // SimpleFetchApiImplementation), so every real-time gate in the harness orders exactly.
        private static double SecondsSinceProcessStart => (System.DateTime.UtcNow - ProcessEpoch.StartUtc).TotalSeconds;
        private static float readyWait;
        private static int lockstepTarget;
        private static int lockstepElapsed;
        private static Vector4 pinnedTime;
        private static Vector4 pinnedSinTime;
        private static Vector4 pinnedCosTime;
        private static Vector4 pinnedTimeParameters;
        private static Vector4 pinnedDeltaTime;
        private static float freezeAtAbs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Environment.GetEnvironmentVariable("DCL_PLAZABENCH_DETERM_CLOCK") != "1")
                return;

            readyWait = EnvFloat("DCL_GOLDEN_READY_WAIT", 130f);
            lockstepTarget = EnvInt("DCL_GOLDEN_FREEZE_FRAME", 180);
            freezeAtAbs = EnvFloat("DCL_GOLDEN_FREEZE_AT", 0f);

            // Early seed so load-time random consumers (procedural scatter, per-instance noise
            // offsets) draw the same sequence every boot; reseeded again at the freeze point.
            UnityEngine.Random.InitState(8262026);

            // Streamed mip residency is filled in load-completion order, so the sampled mip level
            // of any given texture is boot-dependent (visible as per-boot speckle on minified
            // content and flat top-mip colors on starved textures). Force everything resident.
            Texture.streamingTextureForceLoadAll = true;


            // The client auto-selects a quality level from detected hardware, which differs across
            // machines and changes the whole post/shadow stack; goldens must render one fixed level.
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);

            // One-shot copies into texture-array slots (avatar wearables) race the streaming:
            // a copy taken before the source's top mip is resident bakes the blurry low-mip
            // content into the slot forever. With streaming off entirely, every source is
            // complete by the time any copy reads it. Must follow SetQualityLevel — applying
            // a preset writes the preset's own streaming toggle.
            QualitySettings.streamingMipmapsActive = false;

            // Multisample coverage and resolve at silhouettes is the one rasterizer behavior that
            // differs observably across GPU generations; goldens render single-sampled. The backbuffer
            // sample count is pinned alongside the asset: URP mirrors the asset into
            // QualitySettings.antiAliasing only while constructing the pipeline, and Metal renders a
            // black frame whenever 1x camera targets are blitted onto a multisampled drawable.
            QualitySettings.antiAliasing = 0;
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpAsset)
                urpAsset.msaaSampleCount = 1;

            EnsureShaderTimePin();

            // Diagnostic: rule the SRP Batcher's persistent per-material GPU buffers in or out as
            // the remaining per-boot render divergence (CPU-side material state is proven identical).
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_SRP_BATCH") == "1")
                UnityEngine.Rendering.GraphicsSettings.useScriptableRenderPipelineBatching = false;

            // Tweens advance on the same fixed per-frame delta as the deterministic scene clock,
            // so tween completions land on boot-independent frame offsets and the scene's
            // reaction-to-completion vs clock-ceiling ordering is identical every run. Manual
            // update also hard-freezes every tween once the harness stops pumping it.
            DG.Tweening.DOTween.defaultUpdateType = DG.Tweening.UpdateType.Manual;

            pinLods = Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_LODSTREAM") == "1";
            startRealtime = Time.realtimeSinceStartup;
            armed = true;
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            Debug.Log($"[GoldenFreeze] ARMED (URP hook) readyWait={readyWait} lockstep={lockstepTarget}");
        }

        private static void OnBeginRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (!armed) return;

            // The runtime quality tuner rewrites this at will during load; a single window
            // with streaming back on lets a texture-array copy bake a low-mip source.
            QualitySettings.streamingMipmapsActive = false;

            if (!frozen)
                PumpTweensManually();

            if (frozen)
            {
                // Global copies for anything that samples shader time outside a URP camera pass.
                // The camera passes themselves are covered by PinnedShaderTimeFeature: URP rewrites
                // all six time globals from Time.time inside every camera's setup pass, after this
                // hook has run. _TimeParameters is what Shader Graph's Time node reads.
                Shader.SetGlobalVector("_Time", pinnedTime);
                Shader.SetGlobalVector("_SinTime", pinnedSinTime);
                Shader.SetGlobalVector("_CosTime", pinnedCosTime);
                Shader.SetGlobalVector("_TimeParameters", pinnedTimeParameters);
                Shader.SetGlobalVector("_LastTimeParameters", pinnedTimeParameters);
                Shader.SetGlobalVector("unity_DeltaTime", pinnedDeltaTime);

                // The dynamic performance system keeps re-tuning the LOD bias per machine; win the
                // fight by reasserting right before every rendered frame.
                QualitySettings.lodBias = 2f;
                QualitySettings.maximumLODLevel = 0;
                QualitySettings.streamingMipmapsActive = false;

                PinSkyboxNoon();
                PinEnvironmentReflection();
                PinProjection();

                // Only catch animators that something re-enabled (late spawners); the
                // engage rewind plus the spring pin cover everything else, and repeated
                // full rewinds re-roll animation state machines visibly.
                FreezeAnimationSources(onlyActive: true);

                PollRenderDocTrigger();
                return;
            }

            if (!lockstepEngaged)
            {
                // The LOD settings asset is Addressables-provisioned some time after startup, so
                // the representation pin must re-apply until the world is ready: no LOD streaming
                // and an SDK7 threshold beyond any bucket, forcing every scene to load in full.
                if (pinLods)
                    foreach (ScriptableObject so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
                    {
                        if (so.GetType().Name != "LODSettingsAsset") continue;

                        foreach (System.Reflection.PropertyInfo p in so.GetType().GetProperties())
                            if (p.PropertyType == typeof(bool) && p.Name.Contains("Streaming") && p.CanWrite)
                                p.SetValue(so, false);
                    }

                // Generous real-time wait for the world to fully load — engaging captureDeltaTime
                // during load starves the frame loop and stalls loading.
                if (SecondsSinceProcessStart < readyWait)
                    return;

                Time.captureDeltaTime = LOCKSTEP_DELTA;
                QualitySettings.vSyncCount = 0;

                // Re-pin here: the client's settings system re-applies its graphics configuration
                // after startup, overwriting the Init-time value. Backbuffer and asset move together
                // (see Init) so Metal never blits 1x targets onto a multisampled drawable.
                QualitySettings.antiAliasing = 0;
                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpAtEngage)
                    urpAtEngage.msaaSampleCount = 1;

                // LODGroup level selection depends on the runtime lodBias, which performance
                // systems tune per machine; goldens must pick identical LOD levels everywhere.
                QualitySettings.lodBias = 2f;
                QualitySettings.maximumLODLevel = 0;

                PinSkyboxNoon();

                // An active HDR display output rewrites the whole tone curve per-machine
                // (automatic tonemapping); goldens render on the SDR path everywhere.
                if (HDROutputSettings.main.available && HDROutputSettings.main.active)
                    HDROutputSettings.main.RequestHDRModeChange(false);

                // Diagnostic flat mode: no shadows, no post volumes — isolates how much of a
                // cross-machine diff lives in the shadow/post stack versus base shading.
                if (Environment.GetEnvironmentVariable("DCL_GOLDEN_FLAT") == "1")
                {
                    if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urpFlat)
                        urpFlat.shadowDistance = 0f;

                    foreach (UnityEngine.Rendering.Volume vol in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        vol.weight = 0f;

                    // Anisotropic filtering weight distribution is GPU-implementation-defined;
                    // it dominates oblique high-frequency surfaces (the tiled floor).
                    QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
                }

                // Seeks are asynchronous — park media here so the sought frame is decoded and
                // presented before the freeze lands (it is parked again at the freeze point).
                ParkMediaPlayers();

                lockstepEngaged = true;
                engagedAt = SecondsSinceProcessStart;
                Debug.Log("[GoldenFreeze] ready-wait done; lockstep engaged");
                return;
            }

            if (++lockstepElapsed < lockstepTarget)
                return;

            // Quiescence gate: late async loaders (event thumbnails, streamed media posters) can
            // land a texture right around the freeze, making its presence a per-boot race. Only
            // freeze once the texture population has held still for several consecutive samples.
            if (lockstepElapsed % 30 == 0)
            {
                int textureCount = Resources.FindObjectsOfTypeAll<Texture2D>().Length;

                if (textureCount != lastTextureCount)
                {
                    lastTextureCount = textureCount;
                    stableTextureSamples = 0;
                }
                else
                    stableTextureSamples++;
            }

            // Bounded in real seconds: pipelines that create textures continuously (raw-GLTF
            // conversion churn) would otherwise hold the freeze off forever; past the timeout the
            // absolute freeze floor is the settle guarantee instead. A frame-count bound would
            // scale with the machine's frame rate, which is exactly what these gates must not do.
            if (stableTextureSamples < 3 && SecondsSinceProcessStart < engagedAt + 60.0)
                return;

            // DCL_GOLDEN_FREEZE_AT is a REAL-time floor: the phase-locked scene clocks settle on
            // the wall clock (bootstrap ticks, real-time hold, real-paced resume to the ceiling),
            // so the freeze must wait for wall time, not game time — game time races under
            // captureDeltaTime at machine-dependent frame rates. Everything phase-bearing that
            // the freeze captures is pinned at the freeze itself, so the exact frame chosen only
            // needs the quantum gate below for a stable game-time anchor.
            if (freezeAtAbs > 0f && SecondsSinceProcessStart < freezeAtAbs)
                return;

            // Land game time EXACTLY on the quantum boundary: the crossing frame's residue
            // (time % quantum, up to one lockstep step) is boot-random and every
            // game-time-driven phase - idle breathing above all - inherits it. Stepping the
            // last pre-boundary frame by the exact remainder collapses the residue to float
            // rounding, identical every boot.
            double phase = Time.timeAsDouble % FREEZE_QUANTUM;

            if (phase >= LOCKSTEP_DELTA)
            {
                double rem = FREEZE_QUANTUM - phase;
                Time.captureDeltaTime = rem <= LOCKSTEP_DELTA ? (float)rem : LOCKSTEP_DELTA;
                return;
            }

            if (phase > 0.0001)
                return;

            foreach (Camera cam in Camera.allCameras)
                if (cam.TryGetComponent(out UniversalAdditionalCameraData data))
                    data.antialiasing = AntialiasingMode.None;

            NormalizeStochasticState();
            ProbeTerrainDetails(Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_DETAILS") == "1");

            // Diagnostic: kill the instanced ground draw (RenderGroundSystem guards on the material).
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_GROUND") == "1")
                foreach (ScriptableObject so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
                {
                    try
                    {
                        if (so == null || so.GetType().Name != "LandscapeData") continue;
                        WriteMember(so, "GroundMaterial", null);
                    }
                    catch (Exception) { /* best-effort */ }
                }

            // Diagnostic: zero every plausible GPUI renderer's instance count — if a residual
            // object disappears from the frame, it is drawn by the GPUI indirect path.
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_GPUI") == "1")
                for (var key = 0; key < 256; key++)
                {
                    try { GPUInstancerPro.GPUICoreAPI.SetInstanceCount(key, 0); }
                    catch (Exception) { /* unknown keys throw or no-op */ }
                }

            ProbeGpuInstancingMaterials();

            // Regenerate the skybox-derived environment (ambient + default reflection cubemap)
            // from the noon-pinned skybox. Unity's automatic regeneration is timing-dependent —
            // and never fires at all on the Linux Vulkan player, leaving UnityBlackCube as the
            // environment reflection, which removes the sky tint from every smooth surface.
            DynamicGI.UpdateEnvironment();

            const float t = FROZEN_SHADER_TIME;
            pinnedTime = new Vector4(t / 20f, t, t * 2f, t * 3f);
            pinnedSinTime = new Vector4(Mathf.Sin(t / 8f), Mathf.Sin(t / 4f), Mathf.Sin(t / 2f), Mathf.Sin(t));
            pinnedCosTime = new Vector4(Mathf.Cos(t / 8f), Mathf.Cos(t / 4f), Mathf.Cos(t / 2f), Mathf.Cos(t));
            pinnedTimeParameters = new Vector4(t, Mathf.Sin(t), Mathf.Cos(t), 0f);

            // The frozen frame's own delta is 0 (URP would publish 1/0); the lockstep step keeps every
            // delta-driven shader on the same finite value it saw during the lockstep window.
            pinnedDeltaTime = new Vector4(LOCKSTEP_DELTA, 1f / LOCKSTEP_DELTA, LOCKSTEP_DELTA, 1f / LOCKSTEP_DELTA);
            EnsureShaderTimePin();

            Time.captureDeltaTime = 0f;
            Time.timeScale = 0f;
            frozen = true;
            AppDomain.CurrentDomain.SetData("golden.frozen", true);
            Debug.Log($"[GoldenFreeze] FROZEN at time={Time.time:F2}");
        }

        /// <summary>
        ///     Replaces accumulated-since-boot stochastic state with state derived from constants:
        ///     particles re-simulate from a fixed seed for a fixed duration (instead of freezing a
        ///     random mid-flight configuration), animators snap to the start of their current cycle,
        ///     and the global random stream is reseeded.
        /// </summary>
        private static void NormalizeStochasticState()
        {
            UnityEngine.Random.InitState(8262026);

            // All SDK tweens run on DOTween; snapping every live tween to phase 0 removes the
            // boot-dependent offset a looping tween carries from its creation time.
            NormalizeTweens(DG.Tweening.DOTween.PlayingTweens());
            NormalizeTweens(DG.Tweening.DOTween.PausedTweens());

            foreach (UnityEngine.Video.VideoPlayer vp in UnityEngine.Object.FindObjectsByType<UnityEngine.Video.VideoPlayer>(FindObjectsSortMode.None))
            {
                try
                {
                    vp.Pause();
                    vp.frame = 0;
                }
                catch (Exception) { /* best-effort per player */ }
            }

            ParkMediaPlayers();

            // Nametag glyphs rasterize from the TMP dynamic atlas, whose packing follows
            // profile-resolution order; with many tags on screen every label carries
            // per-glyph UV drift. Dropping the flag makes NameTagCleanUpSystem remove
            // all tags on its next update, before capture polling begins.
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_NAMETAGS") == "1")
                foreach (DCL.Nametags.NametagsData tags in Resources.FindObjectsOfTypeAll<DCL.Nametags.NametagsData>())
                    tags.showNameTags = false;

            foreach (ParticleSystem ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            {
                try
                {
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    ps.useAutoRandomSeed = false;
                    ps.randomSeed = 826u;
                    ps.Simulate(2f, false, true, true);
                    ps.Pause(false);
                }
                catch (Exception) { /* best-effort per system */ }
            }

            // LOD selection follows each group's evaluated screen height, whose reference
            // bounds inherit mesh-combine order; a group sitting on a threshold flips LODs
            // between boots. Forcing the finest LOD removes the selection axis entirely.
            foreach (LODGroup g in UnityEngine.Object.FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                try { g.ForceLOD(0); }
                catch (Exception) { /* best-effort per group */ }
            }

            // Shadow maps render off-frustum content, so a caster behind the camera whose
            // per-boot state flips lands a phantom shadow in-frame while every scene-graph
            // probe reads identical. With shadows off the frame is caster-independent — and
            // in the golden views shadows contribute no other visible pixels.
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_NO_SHADOWS") == "1")
                foreach (Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    l.shadows = LightShadows.None;

            // Where two opaque surfaces are exactly co-planar, the draw-order tie-break —
            // renderer creation order, boot-dependent — picks the visible one. The opaque
            // pass sorts by CommonOpaque, where the material render queue is the leading
            // key (rendererPriority is not consulted), so a queue offset derived only from
            // the renderer's path and bounds fixes the order every run. Transparent queues
            // keep their order — reshuffling blending is a visible change, not a tie-break.
            if (Environment.GetEnvironmentVariable("DCL_GOLDEN_STABLE_ORDER") == "1")
                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    try
                    {
                        unchecked
                        {
                            var h = 2166136261u;

                            foreach (char ch in Path(r.transform))
                                h = (h ^ ch) * 16777619u;

                            Vector3 c = r.bounds.center;
                            h = (h ^ (uint)Mathf.RoundToInt(c.x * 8f)) * 16777619u;
                            h = (h ^ (uint)Mathf.RoundToInt(c.y * 8f)) * 16777619u;
                            h = (h ^ (uint)Mathf.RoundToInt(c.z * 8f)) * 16777619u;
                            // Positive-only: the opaque pass covers queues 2000-2500, so a
                            // negative offset on a base-2000 material would drop its draw
                            // out of the pass entirely. 2450+47 stays inside the range.
                            var offset = (int)(h % 48u);

                            foreach (Material m in r.materials)
                                if (m != null && m.renderQueue >= 2000 && m.renderQueue < 2452)
                                    m.renderQueue += offset;
                        }
                    }
                    catch (Exception) { /* best-effort per renderer */ }
                }

            // Avatar toon materials never upload _MatCap_Sampler_ST, so its cbuffer bytes
            // are stale upload-ring garbage that varies per boot and feeds TRANSFORM_TEX on
            // the matcap UV. Writing the vector explicitly registers the property on the
            // material so a defined identity value is uploaded every draw. Per-draw light
            // probe coefficients also vary per boot; anchoring every renderer to the global
            // ambient probe removes that input.
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                try
                {
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

                    foreach (Material m in r.sharedMaterials)
                        if (m != null && m.shader != null && m.shader.name.StartsWith("DCL/DCL_"))
                            m.SetVector("_MatCap_Sampler_ST", new Vector4(1f, 1f, 0f, 0f));
                }
                catch (Exception) { /* best-effort per renderer */ }
            }

            FreezeAnimationSources(onlyActive: false);

            WriteFreezeReport();
        }

        /// <summary>
        ///     The runtime logger is silenced after bootstrap, so the freeze diagnostics go to a file:
        ///     everything that was normalized (tweens with their targets, animators, legacy animations,
        ///     particle systems) plus every renderer material with an unassigned texture property —
        ///     the fingerprint of content that samples undefined memory and breaks reproducibility.
        /// </summary>
        private static readonly bool HARD_FREEZE_ANIMATORS = Environment.GetEnvironmentVariable("DCL_GOLDEN_FREEZE_ANIMATORS") == "1";

        /// <summary>
        ///     Rewinds and stops every animation source. Avatars that finish instantiating
        ///     after the freeze arrive with fresh enabled animators, so the frozen path
        ///     re-applies this each frame with onlyActive=true to catch newcomers cheaply.
        /// </summary>
        private static void FreezeAnimationSources(bool onlyActive)
        {
            // Animation-rigging constraints (feet/hands/head IK) evaluate after the
            // animator with weights and targets smoothed over wall time; the rewound
            // pose would re-apply whatever offsets each boot happened to hold. Zeroed
            // weights make Update(0) evaluate the pure animator pose.
            if (HARD_FREEZE_ANIMATORS)
                foreach (UnityEngine.Animations.Rigging.Rig rig in UnityEngine.Object.FindObjectsByType<UnityEngine.Animations.Rigging.Rig>(FindObjectsSortMode.None))
                {
                    try { if (rig.weight != 0f) rig.weight = 0f; }
                    catch (Exception) { /* best-effort per rig */ }
                }

            foreach (Animator anim in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
            {
                try
                {
                    if (onlyActive && !anim.enabled) continue;

                    // Every step guarded on its own: a throw in any earlier step must never
                    // skip the disable, or that animator keeps re-driving its bones with the
                    // wall-dependent pose it happened to hold.

                    // Bones the current states do not key keep whatever the last transient
                    // pose left in them; writing defaults first lands every animated value
                    // on its authored default before the rewound states overlay theirs.
                    try { anim.WriteDefaultValues(); }
                    catch (Exception) { /* best-effort */ }

                    for (int layer = 0; layer < anim.layerCount; layer++)
                        try { anim.Play(anim.GetCurrentAnimatorStateInfo(layer).fullPathHash, layer, 0f); }
                        catch (Exception) { /* best-effort per layer */ }

                    try { anim.Update(0f); }
                    catch (Exception) { /* best-effort */ }

                    // A rewound-but-enabled animator can be re-driven by systems that keep
                    // running after the freeze; disabled, its bones hold the rewound pose.
                    // Scaled update mode makes re-enablement harmless too: with timeScale
                    // zero an enabled animator advances by exactly nothing.
                    anim.updateMode = AnimatorUpdateMode.Normal;
                    if (HARD_FREEZE_ANIMATORS) anim.enabled = false;
                    anim.speed = 0f;
                }
                catch (Exception) { /* best-effort per animator */ }
            }

            // GLTF-imported looping clips play through legacy Animation components, whose phase is
            // wall-clock-anchored; sample them all at clip start so every run renders the same pose.
            foreach (Animation anim in UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsSortMode.None))
            {
                try
                {
                    if (onlyActive && !anim.isPlaying) continue;

                    foreach (AnimationState st in anim)
                    {
                        // The sun cycle must agree with the pinned noon skybox; every other clip
                        // samples its start.
                        st.time = st.clip != null && st.clip.name == "DirectionalLightCycle" ? st.length * 0.5f : 0f;
                        st.speed = 0f;
                    }

                    anim.Sample();
                }
                catch (Exception) { /* best-effort per animation */ }
            }
        }

        private static void WriteFreezeReport()
        {
            try
            {
                var sb = new System.Text.StringBuilder(64 * 1024);
                sb.AppendLine($"time={Time.timeAsDouble:F4} frame={Time.frameCount}");
                sb.AppendLine($"quality={QualitySettings.GetQualityLevel()}/{QualitySettings.names.Length - 1} ({QualitySettings.names[QualitySettings.GetQualityLevel()]}) lodBias={QualitySettings.lodBias:F2} maxLOD={QualitySettings.maximumLODLevel} aniso={QualitySettings.anisotropicFiltering}");

                if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                    sb.AppendLine($"urp: gradingMode={urp.colorGradingMode} lutSize={urp.colorGradingLutSize} hdr={urp.supportsHDR} hdrPrecision={urp.hdrColorBufferPrecision} msaa={urp.msaaSampleCount} backbufferAA={QualitySettings.antiAliasing} renderScale={urp.renderScale:F3} upscaling={urp.upscalingFilter}");

                foreach (var fmt in new[]
                         {
                             UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat,
                             UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32,
                             UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,
                         })
                    sb.AppendLine($"fmt {fmt}: render={SystemInfo.IsFormatSupported(fmt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render)} blend={SystemInfo.IsFormatSupported(fmt, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Blend)}");

                sb.AppendLine($"gfx: {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} hdrDisplay={SystemInfo.hdrDisplaySupportFlags} hdrOut={HDROutputSettings.main.available}/{HDROutputSettings.main.active}");

                // Full lighting-environment dump: any cross-machine tone offset with matching sky
                // pixels comes from one of these inputs (ambient SH, sun, fog, volumes), so the
                // two reports must agree line-for-line for goldens to match.
                sb.AppendLine($"ambient: mode={RenderSettings.ambientMode} intensity={RenderSettings.ambientIntensity:F4} sky={RenderSettings.ambientSkyColor} equator={RenderSettings.ambientEquatorColor} ground={RenderSettings.ambientGroundColor} reflIntensity={RenderSettings.reflectionIntensity:F4} reflBounces={RenderSettings.reflectionBounces}");
                UnityEngine.Rendering.SphericalHarmonicsL2 ambientSh = RenderSettings.ambientProbe;
                var shSb = new System.Text.StringBuilder("ambientSH:");
                for (var c = 0; c < 3; c++)
                for (var k = 0; k < 9; k++)
                    shSb.Append($" {ambientSh[c, k]:F5}");
                sb.AppendLine(shSb.ToString());
                sb.AppendLine($"fog: enabled={RenderSettings.fog} mode={RenderSettings.fogMode} color={RenderSettings.fogColor} density={RenderSettings.fogDensity:F5} start={RenderSettings.fogStartDistance:F2} end={RenderSettings.fogEndDistance:F2}");
                Light sun = RenderSettings.sun;
                sb.AppendLine(sun != null
                    ? $"sun: color={sun.color} intensity={sun.intensity:F4} temp={sun.colorTemperature:F1} rot={sun.transform.rotation.ToString("F5")} shadows={sun.shadows} shadowStrength={sun.shadowStrength:F3}"
                    : "sun: null");
                sb.AppendLine($"shadow: distance={QualitySettings.shadowDistance:F2} res={QualitySettings.shadowResolution} cascades={QualitySettings.shadowCascades}");
                sb.AppendLine($"skyboxMat: {(RenderSettings.skybox != null ? RenderSettings.skybox.name : "null")}");

                foreach (UnityEngine.Rendering.Volume vol in UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None))
                    sb.AppendLine($"volume: {vol.name} weight={vol.weight:F4} global={vol.isGlobal} priority={vol.priority:F1} profile={(vol.profile != null ? vol.profile.name : "null")}");

                foreach (Light li in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (li.type == LightType.Directional)
                        sb.AppendLine($"dirLight: {li.name} enabled={li.enabled} color={li.color} intensity={li.intensity:F4} rot={li.transform.rotation.ToString("F5")}");

                DumpReflection(sb);

                // The pipeline block must be comparable across machines even where the report file
                // path is not writable — the player log always carries it.
                Debug.Log($"[GoldenFreeze] pipeline: {sb}");

                var playing = DG.Tweening.DOTween.PlayingTweens();
                var paused = DG.Tweening.DOTween.PausedTweens();
                sb.AppendLine($"tweens playing={playing?.Count ?? 0} paused={paused?.Count ?? 0}");
                DescribeTweens(sb, playing);
                DescribeTweens(sb, paused);

                // The mover region under investigation: renderers here get census lines and
                // animators a full-precision bone dump, so a pose flip shows up as an exact
                // per-bone difference. Overridable per lane as "cx,cy,cz,sx,sy,sz".
                var censusBox = new Bounds(new Vector3(6f, 4f, 5f), new Vector3(18f, 10f, 10f));
                string boxEnv = Environment.GetEnvironmentVariable("DCL_GOLDEN_CENSUS_BOX");

                if (!string.IsNullOrEmpty(boxEnv))
                {
                    string[] parts = boxEnv.Split(',');

                    if (parts.Length == 6
                        && float.TryParse(parts[0], out float cx) && float.TryParse(parts[1], out float cy) && float.TryParse(parts[2], out float cz)
                        && float.TryParse(parts[3], out float sx) && float.TryParse(parts[4], out float sy) && float.TryParse(parts[5], out float sz))
                        censusBox = new Bounds(new Vector3(cx, cy, cz), new Vector3(sx, sy, sz));
                }

                foreach (Animator a in UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None))
                {
                    sb.Append($"animator {Path(a.transform)} pos={a.transform.position} en={a.enabled} spd={a.speed:F3}");

                    for (var li = 0; li < a.layerCount; li++)
                    {
                        AnimatorStateInfo st = a.GetCurrentAnimatorStateInfo(li);
                        sb.Append($" L{li}={st.fullPathHash}@{st.normalizedTime:F4}");
                    }

                    sb.AppendLine();

                    if (censusBox.Contains(a.transform.position))
                    {
                        Vector3 wp = a.transform.position;
                        Quaternion wr = a.transform.rotation;
                        Vector3 ws = a.transform.lossyScale;
                        sb.AppendLine($"aroot {Path(a.transform)} wp=({wp.x:R},{wp.y:R},{wp.z:R}) wr=({wr.x:R},{wr.y:R},{wr.z:R},{wr.w:R}) ws=({ws.x:R},{ws.y:R},{ws.z:R})");

                        Transform[] bones = a.GetComponentsInChildren<Transform>(true);

                        for (var bi = 0; bi < bones.Length; bi++)
                        {
                            Vector3 lp = bones[bi].localPosition;
                            Quaternion lr = bones[bi].localRotation;
                            Vector3 lsc = bones[bi].localScale;
                            Vector3 bwp = bones[bi].position;
                            sb.AppendLine($"bone {Path(a.transform)}#{bi}:{bones[bi].name} lp=({lp.x:R},{lp.y:R},{lp.z:R}) lr=({lr.x:R},{lr.y:R},{lr.z:R},{lr.w:R}) ls=({lsc.x:R},{lsc.y:R},{lsc.z:R}) wp=({bwp.x:R},{bwp.y:R},{bwp.z:R})");
                        }
                    }
                }

                foreach (Animation a in UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsSortMode.None))
                {
                    sb.Append($"animation {Path(a.transform)} pos={a.transform.position} clips=");

                    foreach (AnimationState st in a)
                        sb.Append(st.clip != null ? st.clip.name : "?").Append(',');

                    sb.AppendLine();
                }

                var probeBox = new Bounds(new Vector3(2f, 4f, -10f), new Vector3(28f, 14f, 22f));
                var djBox = new Bounds(new Vector3(24f, 6f, -18f), new Vector3(18f, 12f, 16f));
                var probeTextures = new HashSet<Texture>();
                var mpb = new MaterialPropertyBlock();

                foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (censusBox.Intersects(r.bounds))
                    {
                        string cmesh = r.TryGetComponent(out MeshFilter cmf) && cmf.sharedMesh != null ? cmf.sharedMesh.name
                            : r is SkinnedMeshRenderer csmr && csmr.sharedMesh != null ? csmr.sharedMesh.name : "?";
                        Material cm = r.sharedMaterial;
                        Vector3 bc = r.bounds.center, bs = r.bounds.size;

                        sb.Append(
                            $"census {Path(r.transform)} mesh={cmesh} bc=({bc.x:F3},{bc.y:F3},{bc.z:F3}) bs=({bs.x:F3},{bs.y:F3},{bs.z:F3}) " +
                            $"enabled={r.enabled} active={r.gameObject.activeInHierarchy} visible={r.isVisible} " +
                            $"mat={(cm != null ? cm.name : "null")} q={(cm != null ? cm.renderQueue : -1)} kw={(cm != null ? string.Join("|", cm.shaderKeywords) : "")}");

                        if (cm != null)
                            foreach (string tp in cm.GetTexturePropertyNames())
                            {
                                Texture ct = cm.HasProperty(tp) ? cm.GetTexture(tp) : null;
                                if (ct != null) sb.Append($" {tp}={ct.name}[{ct.width}x{ct.height},{ct.graphicsFormat}]");
                            }

                        sb.AppendLine();
                    }

                    // Full forensics for everything near the noise-suspect region: mesh identity plus
                    // every bound texture with its size and format, to identify content whose pixels
                    // hold uninitialized memory.
                    if (probeBox.Intersects(r.bounds) || djBox.Intersects(r.bounds))
                    {
                        string mesh = r.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null ? mf.sharedMesh.name
                            : r is SkinnedMeshRenderer smr && smr.sharedMesh != null ? smr.sharedMesh.name : "?";

                        r.GetPropertyBlock(mpb);
                        Quaternion rot = r.transform.rotation;
                        Vector3 scl = r.transform.lossyScale;

                        foreach (Material m in r.sharedMaterials)
                        {
                            if (m == null) continue;

                            sb.Append($"probe {Path(r.transform)} bounds={r.bounds.center} rot=({rot.x:F4},{rot.y:F4},{rot.z:F4},{rot.w:F4}) scale=({scl.x:F3},{scl.y:F3},{scl.z:F3}) mesh={mesh} shader={m.shader.name} ");

                            Shader sh = m.shader;

                            for (var pi = 0; pi < sh.GetPropertyCount(); pi++)
                            {
                                string pn = sh.GetPropertyName(pi);

                                switch (sh.GetPropertyType(pi))
                                {
                                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                                        sb.Append($"{pn}={m.GetFloat(pn):F4} ");
                                        break;
                                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                                        Color c = m.GetColor(pn);
                                        sb.Append($"{pn}=({c.r:F3},{c.g:F3},{c.b:F3},{c.a:F3}) ");
                                        break;
                                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                                        Vector4 v = m.GetVector(pn);
                                        sb.Append($"{pn}=({v.x:F3},{v.y:F3},{v.z:F3},{v.w:F3}) ");
                                        break;
                                }
                            }

                            foreach (string prop in m.GetTexturePropertyNames())
                            {
                                // The property block override, when present, is what actually renders.
                                Texture tex = mpb.GetTexture(prop);
                                if (tex == null) tex = m.HasProperty(prop) ? m.GetTexture(prop) : null;

                                if (tex != null)
                                {
                                    Vector2 off = m.GetTextureOffset(prop);
                                    Vector2 tsc = m.GetTextureScale(prop);
                                    sb.Append($"{prop}={tex.name}[{tex.width}x{tex.height},{tex.graphicsFormat},mips={tex.mipmapCount}]sha={ContentSha(tex)}st=({off.x:F4},{off.y:F4},{tsc.x:F4},{tsc.y:F4}) ");
                                    probeTextures.Add(tex);
                                }
                            }

                            sb.AppendLine();
                        }
                    }

                    foreach (Material m in r.sharedMaterials)
                    {
                        if (m == null) continue;

                        foreach (string prop in m.GetTexturePropertyNames())
                            if (m.HasProperty(prop) && m.GetTexture(prop) == null)
                                sb.AppendLine($"null-tex {Path(r.transform)} bounds={r.bounds.center} mat={m.name} shader={m.shader.name} prop={prop}");
                    }
                }

                // Ray through the residual-noise pixel (viewport 0.258, 0.542 of the golden frame):
                // every renderer whose bounds the ray crosses is a candidate owner of those pixels;
                // hashing their mesh buffers tests the one render input not otherwise compared.
                Camera rayCam = Camera.main;

                if (rayCam != null)
                {
                    Ray blobRay = rayCam.ViewportPointToRay(new Vector3(0.258f, 0.542f, 0f));

                    foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    {
                        if (!r.isVisible || !r.bounds.IntersectRay(blobRay, out float dist) || dist > 60f) continue;

                        Mesh mesh = r.TryGetComponent(out MeshFilter mf2) ? mf2.sharedMesh : (r as SkinnedMeshRenderer)?.sharedMesh;
                        string meshSha = "n/a";

                        if (mesh != null && mesh.isReadable)
                        {
                            try
                            {
                                using var hasher = System.Security.Cryptography.SHA256.Create();
                                Vector3[] verts = mesh.vertices;
                                Vector3[] norms = mesh.normals;
                                Vector4[] tans = mesh.tangents;
                                var bytes = new byte[(verts.Length * 3 + norms.Length * 3 + tans.Length * 4) * 4];
                                Buffer.BlockCopy(verts, 0, bytes, 0, verts.Length * 12);
                                Buffer.BlockCopy(norms, 0, bytes, verts.Length * 12, norms.Length * 12);
                                Buffer.BlockCopy(tans, 0, bytes, verts.Length * 12 + norms.Length * 12, tans.Length * 16);
                                meshSha = BitConverter.ToString(hasher.ComputeHash(bytes)).Replace("-", "").Substring(0, 12);
                            }
                            catch (Exception) { meshSha = "exc"; }
                        }

                        sb.AppendLine($"rayhit d={dist:F1} {Path(r.transform)} mesh={mesh?.name ?? "?"} meshSha={meshSha} mat={r.sharedMaterial?.shader.name}");
                    }
                }

                // Byte-level dump of every texture near the noise-suspect region, in a per-process
                // directory: comparing dumps across two boots separates per-boot texture content
                // (conversion/decode nondeterminism) from sampling artifacts over stable content.
                string dumpDir = $"/tmp/goldentex-{System.Diagnostics.Process.GetCurrentProcess().Id}";
                System.IO.Directory.CreateDirectory(dumpDir);

                foreach (Texture tex in probeTextures)
                {
                    try
                    {
                        var req = UnityEngine.Rendering.AsyncGPUReadback.Request(tex, 0);
                        req.WaitForCompletion();

                        if (!req.hasError)
                            System.IO.File.WriteAllBytes(
                                $"{dumpDir}/{Sanitize(tex.name)}_{tex.width}x{tex.height}_{(int)tex.graphicsFormat}.bin",
                                req.GetData<byte>().ToArray());
                    }
                    catch (Exception) { /* best-effort per texture */ }
                }

                System.IO.File.WriteAllText(
                    Environment.GetEnvironmentVariable("DCL_GOLDEN_REPORT")
                    ?? TempPath($"goldenfreeze-report-{System.Diagnostics.Process.GetCurrentProcess().Id}.txt"),
                    sb.ToString());
            }
            catch (Exception) { /* diagnostics only */ }
        }

        private static void DescribeTweens(System.Text.StringBuilder sb, List<DG.Tweening.Tween> tweens)
        {
            if (tweens == null) return;

            foreach (DG.Tweening.Tween tw in tweens)
            {
                string target = tw.target?.ToString() ?? "null";
                string where = tw.target is Component c ? $" pos={c.transform.position} path={Path(c.transform)}" : "";
                sb.AppendLine($"tween target={target}{where} duration={DG.Tweening.TweenExtensions.Duration(tw, false)} loops={DG.Tweening.TweenExtensions.Loops(tw)}");
            }
        }

        /// <summary>
        ///     The landscape scatter details (grass/flowers/bushes) draw indirect with no Renderer,
        ///     so they are invisible to every scene-graph probe. Reflect into the detail assets to
        ///     record what they are — and, when disabling, null their materials, which the scatter
        ///     renderer treats as "this detail does not render".
        /// </summary>
        private static void ProbeTerrainDetails(bool disable)
        {
            var sb = new System.Text.StringBuilder();

            foreach (ScriptableObject so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                // Per-item guard: one hostile type (ambiguous member, throwing getter) must not
                // silently abort the whole probe — that regression disabled the nulling unnoticed.
                try
                {
                    if (so == null || so.GetType().Name != "LandscapeAsset") continue;

                    object settings = so.GetType().GetProperty("TerrainDetailSettings")?.GetValue(so)
                                      ?? so.GetType().GetField("TerrainDetailSettings")?.GetValue(so);
                    if (settings == null) continue;

                    var mesh = ReadMember(settings, "Mesh") as Mesh;
                    var mat = ReadMember(settings, "Material") as Material;
                    sb.AppendLine($"detail {so.name} mesh={mesh?.name ?? "null"} mat={mat?.name ?? "null"} shader={mat?.shader.name ?? "null"}");

                    if (disable)
                        WriteMember(settings, "Material", null);
                }
                catch (Exception) { /* best-effort per asset */ }
            }

            TryWriteTemp($"goldendetails-{System.Diagnostics.Process.GetCurrentProcess().Id}.txt", sb.ToString());
        }

        private const System.Reflection.BindingFlags ANY_MEMBER =
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        private static object ReadMember(object obj, string name) =>
            obj.GetType().GetProperty(name, ANY_MEMBER)?.GetValue(obj)
            ?? obj.GetType().GetField(name, ANY_MEMBER)?.GetValue(obj)
            ?? obj.GetType().GetField($"<{name}>k__BackingField", ANY_MEMBER)?.GetValue(obj);

        private static void WriteMember(object obj, string name, object value)
        {
            var prop = obj.GetType().GetProperty(name, ANY_MEMBER);
            if (prop != null && prop.CanWrite) { prop.SetValue(obj, value); return; }

            var field = obj.GetType().GetField(name, ANY_MEMBER)
                        ?? obj.GetType().GetField($"<{name}>k__BackingField", ANY_MEMBER);
            field?.SetValue(obj, value);
        }

        private static readonly Dictionary<Texture, string> contentShaCache = new ();

        private static string ContentSha(Texture tex)
        {
            if (contentShaCache.TryGetValue(tex, out string cached))
                return cached;

            string sha;

            try
            {
                var req = UnityEngine.Rendering.AsyncGPUReadback.Request(tex, 0);
                req.WaitForCompletion();

                if (req.hasError) sha = "readback-error";
                else
                {
                    using var hasher = System.Security.Cryptography.SHA256.Create();
                    sha = BitConverter.ToString(hasher.ComputeHash(req.GetData<byte>().ToArray())).Replace("-", "").Substring(0, 12);
                }
            }
            catch (Exception) { sha = "exc"; }

            contentShaCache[tex] = sha;
            return sha;
        }

        /// <summary>
        ///     "--skybox-time-enabled false" freezes the day cycle wherever it happens to be at
        ///     boot, so the frozen hour — and with it sky tint and ambient — is machine- and
        ///     boot-dependent. Pin exact noon through the UI-override channel, which outranks the
        ///     skybox systems.
        /// </summary>
        /// <summary>
        ///     The projection matrix holds 1/tan(fov/2), and libm tangent rounding differs by an
        ///     ULP between platforms (glibc vs the Windows CRT), shifting every vertex sub-pixel —
        ///     visible only as edge flips on knife-thin geometry. The golden camera always runs at
        ///     fov 60, so the frozen frames use the correctly-rounded literal on every platform;
        ///     the rest of the matrix is exact IEEE arithmetic. Reasserted per frame because the
        ///     engine rewrites the matrix whenever camera state is touched.
        /// </summary>
        private static void PinProjection()
        {
            foreach (Camera cam in Camera.allCameras)
            {
                if (Mathf.Abs(cam.fieldOfView - 60f) > 0.01f || cam.orthographic) continue;

                const float COT_HALF_FOV = 1.7320508075688772f; // 1/tan(30 deg), correctly rounded
                float aspect = cam.aspect;
                float near = cam.nearClipPlane, far = cam.farClipPlane;
                var m = Matrix4x4.zero;
                m.m00 = COT_HALF_FOV / aspect;
                m.m11 = COT_HALF_FOV;
                m.m22 = -(far + near) / (far - near);
                m.m23 = -(2f * far * near) / (far - near);
                m.m32 = -1f;
                cam.projectionMatrix = m;
            }
        }

        private static void PinSkyboxNoon()
        {
            foreach (ScriptableObject so in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (so.GetType().Name != "SkyboxSettingsAsset") continue;

                try
                {
                    so.GetType().GetProperty("IsUIControlled")?.SetValue(so, true);
                    so.GetType().GetProperty("UIOverrideTimeOfDayNormalized")?.SetValue(so, 0.5f);
                    so.GetType().GetProperty("TimeOfDayNormalized")?.SetValue(so, 0.5f);
                }
                catch (Exception) { /* best-effort */ }
            }
        }

        private static string TempPath(string name) => System.IO.Path.Combine(System.IO.Path.GetTempPath(), name);

        private static void TryWriteTemp(string name, string content)
        {
            try { System.IO.File.WriteAllText(TempPath(name), content); }
            catch (Exception) { /* diagnostics only */ }
        }

        private static string Sanitize(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length);

            foreach (char c in name)
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');

            return sb.Length > 0 ? sb.ToString() : "unnamed";
        }

        private static string Path(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);

            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, '/').Insert(0, t.name);
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Scene media surfaces (video billboards) play through the VideoPlayback
        ///     MediaPlayer; park every instance at its first frame.
        /// </summary>
        private static Material? goldenBlackMaterial;

        private static void ParkMediaPlayers()
        {
            var mediaTextures = new System.Collections.Generic.HashSet<Texture>();

            foreach (DCL.VideoPlayback.MediaPlayer mp in UnityEngine.Object.FindObjectsByType<DCL.VideoPlayback.MediaPlayer>(FindObjectsSortMode.None))
            {
                try
                {
                    mp.Control.Pause();
                    mp.Control.Seek(0.0);

                    Texture? produced = mp.TextureProducer.GetTexture();

                    if (produced != null)
                        mediaTextures.Add(produced);
                }
                catch (Exception) { /* best-effort per player */ }
            }

            BlackoutMediaSurfaces(mediaTextures);
        }

        /// <summary>
        ///     Media frames can never match across platforms: decoders differ per OS (VAAPI vs
        ///     MediaFoundation) and the Windows-Vulkan path cannot initialize UUAV at all, leaving
        ///     its display shader erroring to magenta. The only deterministic cross-platform media
        ///     state is a black screen, so every surface showing a media texture — or using a
        ///     UUAV display shader — renders the same flat black material.
        /// </summary>
        private static void BlackoutMediaSurfaces(System.Collections.Generic.HashSet<Texture> mediaTextures)
        {
            try
            {
                if (goldenBlackMaterial == null)
                {
                    Shader? unlit = Shader.Find("Universal Render Pipeline/Unlit");

                    if (unlit == null)
                        return;

                    goldenBlackMaterial = new Material(unlit) { color = Color.black };
                }

                foreach (Renderer rend in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    try
                    {
                        Material[] mats = rend.sharedMaterials;
                        var changed = false;

                        for (var i = 0; i < mats.Length; i++)
                        {
                            Material mat = mats[i];

                            if (mat == null)
                                continue;

                            string shaderName = mat.shader != null ? mat.shader.name : string.Empty;
                            bool isMedia = shaderName.Contains("UUAV", StringComparison.OrdinalIgnoreCase);

                            if (!isMedia && mat.HasProperty("_MainTex") && mat.mainTexture != null)
                                isMedia = mediaTextures.Contains(mat.mainTexture);

                            if (!isMedia && mat.HasProperty("_BaseMap"))
                            {
                                Texture? baseMap = mat.GetTexture("_BaseMap");

                                if (baseMap != null)
                                    isMedia = mediaTextures.Contains(baseMap);
                            }

                            if (isMedia)
                            {
                                mats[i] = goldenBlackMaterial;
                                changed = true;
                            }
                        }

                        if (changed)
                            rend.sharedMaterials = mats;
                    }
                    catch (Exception) { /* best-effort per renderer */ }
                }
            }
            catch (Exception) { /* diagnostics-mode only */ }
        }

        /// <summary>
        ///     Converts every live tween to manual update (covers tweens created before Init or
        ///     with an explicit update type) and advances the whole tween timeline by exactly the
        ///     deterministic scene-tick delta, once per rendered frame. Runs only while unfrozen —
        ///     after the freeze nothing pumps, so all tweens hold their pose.
        /// </summary>
        private static long pumpedTicks;

        /// <summary>
        ///     Tween time must advance in lockstep with the deterministic scene clock, not with
        ///     rendered frames: a per-frame pump runs tween time at frameRate/30 of wall time,
        ///     which is machine- and boot-dependent, so tween completions — and with them the leg
        ///     parity of scene-chained movers — land on nondeterministic ticks. The current scene
        ///     publishes its dispatched tick count (an AppDomain slot avoids an assembly
        ///     reference), and the pump replays exactly one fixed step per elapsed tick.
        /// </summary>
        private static void PumpTweensManually()
        {
            try
            {
                long sceneTicks = AppDomain.CurrentDomain.GetData("golden.currentSceneTicks") is long t ? t : 0L;

                if (sceneTicks <= pumpedTicks) return;

                // Bound catch-up bursts so a stall cannot hitch the frame; the remainder carries.
                long steps = Math.Min(sceneTicks - pumpedTicks, 8L);
                pumpedTicks += steps;

                ConvertTweensToManual(DG.Tweening.DOTween.PlayingTweens());
                ConvertTweensToManual(DG.Tweening.DOTween.PausedTweens());

                for (long i = 0; i < steps; i++)
                    DG.Tweening.DOTween.ManualUpdate(1f / 30f, 1f / 30f);
            }
            catch (Exception) { /* diagnostics-mode only */ }
        }

        private static void ConvertTweensToManual(System.Collections.Generic.List<DG.Tweening.Tween> tweens)
        {
            if (tweens == null)
                return;

            foreach (DG.Tweening.Tween tw in tweens)
            {
                try { DG.Tweening.TweenSettingsExtensions.SetUpdate(tw, DG.Tweening.UpdateType.Manual, true); }
                catch (Exception) { /* best-effort per tween */ }
            }
        }

        private static void NormalizeTweens(System.Collections.Generic.List<DG.Tweening.Tween> tweens)
        {
            if (tweens == null)
                return;

            foreach (DG.Tweening.Tween tw in tweens)
            {
                try
                {
                    // Finite tweens snap to their end pose: whether such a tween is still alive
                    // at freeze depends on its boot-dependent creation time, and a completed
                    // tween leaves the transform at the end — snapping survivors there makes
                    // both cases render identically. Loops snap to phase 0.
                    if (DG.Tweening.TweenExtensions.Loops(tw) >= 0)
                        DG.Tweening.TweenExtensions.Goto(tw, DG.Tweening.TweenExtensions.Duration(tw, true));
                    else
                        DG.Tweening.TweenExtensions.Goto(tw, 0f);
                }
                catch (Exception) { /* best-effort per tween */ }
            }
        }

        private static int EnvInt(string k, int def)
        {
            string v = Environment.GetEnvironmentVariable(k);
            return !string.IsNullOrEmpty(v) && int.TryParse(v, out int r) && r > 0 ? r : def;
        }

        private static float EnvFloat(string k, float def)
        {
            string v = Environment.GetEnvironmentVariable(k);
            return !string.IsNullOrEmpty(v) && float.TryParse(v, out float r) && r > 0 ? r : def;
        }

        /// <summary>
        ///     Diagnostic dump of every GPU-instancing material copy: whether the batcher keyword
        ///     is enabled, and whether the material's (asset-bundle-compiled) shader even declares
        ///     it — a keyword missing from the shader's keyword space means the variant was
        ///     stripped at shader-bundle build and the draw silently falls back to non-instanced.
        /// </summary>
        private static void ProbeGpuInstancingMaterials()
        {
            try
            {
                var sb = new System.Text.StringBuilder();

                foreach (Material mat in Resources.FindObjectsOfTypeAll<Material>())
                {
                    if (mat == null || !mat.name.Contains("_GPUInstancingIndirect")) continue;

                    Shader sh = mat.shader;
                    bool declared = false;

                    foreach (string kw in sh.keywordSpace.keywordNames)
                        if (kw == "_GPU_INSTANCER_BATCHER") { declared = true; break; }

                    sb.AppendLine($"{mat.name} shader={sh.name} enabled={mat.IsKeywordEnabled("_GPU_INSTANCER_BATCHER")} declaredInShader={declared} passCount={mat.passCount}");
                }

                System.IO.File.WriteAllText(
                    TempPath("golden-batcher.txt"),
                    sb.Length > 0 ? sb.ToString() : "no *_GPUInstancingIndirect materials found\n");
            }
            catch (Exception e)
            {
                TryWriteTemp("golden-batcher.txt", "error: " + e);
            }
        }

        private static ReflectionProbe goldenProbe;
        private static int goldenProbeRenderId = -1;
        private static bool reflectionPinned;

        /// <summary>
        ///     Unity's skybox-generated environment reflection never materializes on the Linux
        ///     Vulkan player (the default reflection stays UnityBlackCube even after
        ///     DynamicGI.UpdateEnvironment), so smooth surfaces lose all sky tint there while other
        ///     platforms keep it. Render one scripted probe of the frozen noon world instead and
        ///     pin it as the custom reflection — both platforms then light surfaces from the same
        ///     deterministic source. Spread over frozen frames: RenderProbe is asynchronous.
        /// </summary>
        private static void PinEnvironmentReflection()
        {
            if (reflectionPinned)
                return;

            try
            {
                if (goldenProbe == null)
                {
                    var go = new GameObject("GoldenReflectionProbe");
                    goldenProbe = go.AddComponent<ReflectionProbe>();
                    goldenProbe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                    goldenProbe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                    goldenProbe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                    goldenProbe.resolution = 128;
                    goldenProbe.hdr = true;

                    // Skybox only — no scene geometry. This mirrors what
                    // DefaultReflectionMode.Skybox produces on platforms where it works, and a
                    // pure-sky cubemap is deterministic per boot where a scene render is not.
                    goldenProbe.cullingMask = 0;
                    goldenProbe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.Skybox;

                    Camera cam = Camera.main;

                    if (cam != null)
                        go.transform.position = cam.transform.position;

                    goldenProbeRenderId = goldenProbe.RenderProbe();
                }
                else if (goldenProbeRenderId >= 0 && goldenProbe.IsFinishedRendering(goldenProbeRenderId) && goldenProbe.texture != null)
                {
                    RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;
                    RenderSettings.customReflectionTexture = goldenProbe.texture;
                    reflectionPinned = true;
                    Debug.Log("[GoldenFreeze] environment reflection pinned from golden probe");

                    var pinSb = new System.Text.StringBuilder();
                    DumpReflection(pinSb);
                    TryWriteTemp("golden-reflpin.txt", "pinned\n" + pinSb);
                }
                else if (goldenProbeRenderId >= 0 && Time.frameCount % 300 == 0)
                    TryWriteTemp("golden-reflpin.txt", $"waiting: renderId={goldenProbeRenderId} finished={goldenProbe.IsFinishedRendering(goldenProbeRenderId)} tex={(goldenProbe.texture != null ? goldenProbe.texture.name : "null")}\n");
            }
            catch (Exception) { /* diagnostics-mode only */ }
        }

        /// <summary>
        ///     The custom reflection cubemap tints every smooth surface, so a cross-machine tone
        ///     offset with identical CPU lighting usually lives here: dump its format plus a
        ///     per-face mean color so two reports localize the difference to a face and channel.
        /// </summary>
        private static void DumpReflection(System.Text.StringBuilder sb)
        {
            try
            {
                Texture refl = RenderSettings.customReflectionTexture;

                if (refl == null)
                {
                    // Skybox mode: Unity generates the environment cubemap itself; that generated
                    // texture is what actually tints surfaces, so dump it instead.
                    refl = ReflectionProbe.defaultTexture;
                    sb.AppendLine($"reflection: default-generated (mode={RenderSettings.defaultReflectionMode} res={RenderSettings.defaultReflectionResolution})");

                    if (refl == null)
                    {
                        sb.AppendLine("reflection: defaultTexture null");
                        return;
                    }
                }

                sb.AppendLine($"reflection: {refl.name} dim={refl.dimension} {refl.width}x{refl.height} fmt={refl.graphicsFormat} mips={refl.mipmapCount}");

                var mip = 0;
                while ((refl.width >> mip) > 8 && mip < refl.mipmapCount - 1) mip++;
                int size = Mathf.Max(1, refl.width >> mip);

                var desc = new RenderTextureDescriptor(size, size, refl.graphicsFormat, 0) { mipCount = 1 };
                var tmp = RenderTexture.GetTemporary(desc);
                var readTex = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);

                for (var face = 0; face < 6; face++)
                {
                    Graphics.CopyTexture(refl, face, mip, tmp, 0, 0);
                    RenderTexture prev = RenderTexture.active;
                    RenderTexture.active = tmp;
                    readTex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                    readTex.Apply(false);
                    RenderTexture.active = prev;

                    Color mean = Color.black;
                    Color[] px = readTex.GetPixels();
                    foreach (Color p in px) mean += p;
                    mean /= px.Length;
                    sb.AppendLine($"reflFace{face}: mean=({mean.r:F5},{mean.g:F5},{mean.b:F5})");
                }

                RenderTexture.ReleaseTemporary(tmp);
                UnityEngine.Object.Destroy(readTex);
            }
            catch (Exception e)
            {
                sb.AppendLine("reflection: error " + e.Message);
            }
        }

        private const int RTLD_NOW = 2;
        private const int RTLD_NOLOAD = 4;
        private const int RENDERDOC_API_VERSION_1_1_2 = 10102;

        [System.Runtime.InteropServices.DllImport("libdl.so.2")]
        private static extern IntPtr dlopen(string file, int flags);

        [System.Runtime.InteropServices.DllImport("libdl.so.2")]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int RenderDocGetApi(int version, out IntPtr api);

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate void RenderDocVoidFn();

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate uint RenderDocUintFn();

        // char* argument passed as a manually-allocated ANSI buffer: default string marshaling
        // reached librenderdoc as a wide string, truncating the path at its first character.
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate void RenderDocSetPathFn(IntPtr template);

        private static bool rdocTriggered;

        /// <summary>
        ///     While frozen, a flag file requests a RenderDoc frame capture through the in-application
        ///     API of the already-injected librenderdoc (the Vulkan capture layer loads it into this
        ///     process; RTLD_NOLOAD only ever attaches to that copy — a fresh load could not capture).
        ///     Function-table indices follow renderdoc_app.h RENDERDOC_API_1_1_2: 11 =
        ///     SetCaptureFilePathTemplate, 13 = GetNumCaptures, 15 = TriggerCapture.
        /// </summary>
        private static void PollRenderDocTrigger()
        {
            if (rdocTriggered)
                return;

            string flag = TempPath("golden-rdoc-trigger");

            if (!System.IO.File.Exists(flag))
                return;

            rdocTriggered = true;
            string status;

            try
            {
                System.IO.File.Delete(flag);

                IntPtr lib = dlopen("librenderdoc.so", RTLD_NOW | RTLD_NOLOAD);

                if (lib == IntPtr.Zero)
                    status = "librenderdoc.so not loaded in-process (capture layer inactive)";
                else
                {
                    IntPtr getApiPtr = dlsym(lib, "RENDERDOC_GetAPI");

                    if (getApiPtr == IntPtr.Zero)
                        status = "RENDERDOC_GetAPI symbol missing";
                    else
                    {
                        var getApi = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<RenderDocGetApi>(getApiPtr);

                        if (getApi(RENDERDOC_API_VERSION_1_1_2, out IntPtr api) != 1 || api == IntPtr.Zero)
                            status = "RENDERDOC_GetAPI returned failure";
                        else
                        {
                            string dir = TempPath("golden-rdoc");
                            System.IO.Directory.CreateDirectory(dir);

                            IntPtr setPathPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(api, 11 * IntPtr.Size);
                            IntPtr numCapsPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(api, 13 * IntPtr.Size);
                            IntPtr triggerPtr = System.Runtime.InteropServices.Marshal.ReadIntPtr(api, 15 * IntPtr.Size);

                            IntPtr pathAnsi = System.Runtime.InteropServices.Marshal.StringToHGlobalAnsi(
                                System.IO.Path.Combine(dir, "crown"));

                            try
                            {
                                System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<RenderDocSetPathFn>(setPathPtr)(pathAnsi);
                            }
                            finally
                            {
                                System.Runtime.InteropServices.Marshal.FreeHGlobal(pathAnsi);
                            }

                            System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<RenderDocVoidFn>(triggerPtr)();

                            uint already = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<RenderDocUintFn>(numCapsPtr)();
                            status = $"triggered (captures so far: {already}, template {dir}/crown)";
                        }
                    }
                }
            }
            catch (Exception e)
            {
                status = "error: " + e.Message;
            }

            TryWriteTemp("golden-rdoc-status.txt", status);
        }

        /// <summary>
        ///     Installs <see cref="PinnedShaderTimeFeature"/> on every renderer of the active URP asset,
        ///     once per renderer. Called at arm time and again at the freeze: the active asset can change
        ///     in between when the client applies a quality preset.
        /// </summary>
        private static void EnsureShaderTimePin()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset asset)
                return;

            foreach (ScriptableRendererData data in asset.rendererDataList)
            {
                if (data == null || data.rendererFeatures.Exists(static f => f is PinnedShaderTimeFeature))
                    continue;

                var feature = ScriptableObject.CreateInstance<PinnedShaderTimeFeature>();
                feature.name = nameof(PinnedShaderTimeFeature);
                data.rendererFeatures.Add(feature);
                data.SetDirty();
            }
        }

        /// <summary>
        ///     URP writes its six time globals (_Time, _SinTime, _CosTime, _TimeParameters,
        ///     _LastTimeParameters, unity_DeltaTime) from Time.time in each camera's setup pass, and repeats
        ///     that setup after the shadow passes and around a depth-normals prepass, so a global set from
        ///     the pipeline-begin hook never reaches a draw. While frozen, the pinned values are re-issued
        ///     from a pass placed right after each of those points.
        /// </summary>
        private sealed class PinnedShaderTimeFeature : ScriptableRendererFeature
        {
            private static readonly RenderPassEvent[] POINTS =
            {
                RenderPassEvent.BeforeRenderingShadows,
                RenderPassEvent.AfterRenderingShadows,
                RenderPassEvent.BeforeRenderingOpaques,
            };

            private PinnedShaderTimePass[] passes;

            public override void Create()
            {
                passes = new PinnedShaderTimePass[POINTS.Length];

                for (var i = 0; i < POINTS.Length; i++)
                    passes[i] = new PinnedShaderTimePass { renderPassEvent = POINTS[i] };
            }

            public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
            {
                if (!frozen) return;

                foreach (PinnedShaderTimePass pass in passes)
                    renderer.EnqueuePass(pass);
            }
        }

        private sealed class PinnedShaderTimePass : ScriptableRenderPass
        {
            private static readonly int TIME_ID = Shader.PropertyToID("_Time");
            private static readonly int SIN_TIME_ID = Shader.PropertyToID("_SinTime");
            private static readonly int COS_TIME_ID = Shader.PropertyToID("_CosTime");
            private static readonly int TIME_PARAMETERS_ID = Shader.PropertyToID("_TimeParameters");
            private static readonly int LAST_TIME_PARAMETERS_ID = Shader.PropertyToID("_LastTimeParameters");
            private static readonly int DELTA_TIME_ID = Shader.PropertyToID("unity_DeltaTime");

            private sealed class PassData { }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("GoldenFreeze.PinShaderTime", out PassData _);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc(static (PassData _, UnsafeGraphContext context) =>
                {
                    context.cmd.SetGlobalVector(TIME_ID, pinnedTime);
                    context.cmd.SetGlobalVector(SIN_TIME_ID, pinnedSinTime);
                    context.cmd.SetGlobalVector(COS_TIME_ID, pinnedCosTime);
                    context.cmd.SetGlobalVector(TIME_PARAMETERS_ID, pinnedTimeParameters);
                    context.cmd.SetGlobalVector(LAST_TIME_PARAMETERS_ID, pinnedTimeParameters);
                    context.cmd.SetGlobalVector(DELTA_TIME_ID, pinnedDeltaTime);
                });
            }
        }
    }
}
