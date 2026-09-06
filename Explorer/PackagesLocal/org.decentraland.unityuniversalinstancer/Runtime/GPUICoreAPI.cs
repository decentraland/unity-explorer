// Static facade the DCL landscape code calls into. The namespace and the
// public method shapes are the contract the call sites bind to — LandscapePlugin
// awaits RegisterRenderer, TreeData calls SetInstanceCount /
// SetTransformBufferData, GPUIRuntimeSettingsLoader calls ApplyRuntimeSettings
// (REQ-029, REQ-030). All real state lives in TreeRendererService.

using System.Collections.Generic;
using UnityEngine;

namespace GPUInstancerPro
{
    public static class GPUICoreAPI
    {
        private static TreeRendererService service;
        private static bool lifetimeHooksInstalled;
        private static int lastRenderedFrame = -1;

        private static TreeRendererService Service
        {
            get
            {
                if (service == null)
                {
                    service = new TreeRendererService();
                    InstallLifetimeHooks();
                }
                return service;
            }
        }

        // LandscapePlugin.cs:90-91 — once at app init, per tree prototype.
        public static void RegisterRenderer(Transform root, GameObject prototype, GPUIProfile profile, out int rendererKey)
            => rendererKey = Service.Register(root, prototype, profile);

        // Async registration overload matching the signature the integration
        // awaits: it reads RegisterRendererResult.rendererKey. Registration is
        // synchronous here, so the returned task is already completed.
        public static System.Threading.Tasks.Task<RegisterRendererResult> RegisterRenderer(Transform root, GameObject prototype, GPUIProfile profile)
        {
            RegisterRenderer(root, prototype, profile, out int rendererKey);
            return System.Threading.Tasks.Task.FromResult(new RegisterRendererResult(rendererKey));
        }

        public readonly struct RegisterRendererResult
        {
            public readonly int rendererKey;

            public RegisterRendererResult(int rendererKey)
            {
                this.rendererKey = rendererKey;
            }
        }

        // TreeData.cs:118 (Hide) and :362 (Show).
        public static void SetInstanceCount(int rendererKey, int count)
            => Service.SetInstanceCount(rendererKey, count);

        // TreeData.cs:151 (Instantiate) and :198 (InstantiateAsync).
        public static void SetTransformBufferData(int rendererKey, List<Matrix4x4> matrices, int bufferStart, int matricesStart, int count)
            => Service.SetTransformBufferData(rendererKey, matrices, bufferStart, matricesStart, count);

        // GPUIRuntimeSettingsLoader.Awake — single boot-time hand-off of the
        // SO-authored runtime settings. Only instancingBoundsSize is acted on
        // (GPUIRuntimeSettings.asset authors it as {1000, 1000, 1000}, used as
        // the fallback world-bounds volume); the remaining authored fields are
        // accepted and ignored so the call site keeps its shape.
        public static void ApplyRuntimeSettings(GPUIRuntimeSettings settings)
        {
            if (settings == null) return;
            TreeRendererService.DefaultWorldBoundsSize = settings.instancingBoundsSize;
        }

        // ---- Internal hooks ----

        internal static void NotifyProfileFlushed(GPUIProfile profile) => Service.OnProfileFlushed(profile);

        // Animated LOD cross-fades advance once per frame however many
        // cameras drive it.
        internal static void RenderFrameForCamera(Camera camera)
        {
            int frame = Time.frameCount;
            float deltaTime = frame == lastRenderedFrame ? 0f : Time.deltaTime;
            lastRenderedFrame = frame;
            Service.RenderFrame(camera, deltaTime);
        }

        /// <summary>
        /// Layers the registered prototypes draw on, or 0 when nothing is
        /// registered. Reads through without creating the service so the
        /// per-camera render gate stays free before the first registration.
        /// </summary>
        internal static int RegisteredDrawLayerMask => service?.DrawLayerMask ?? 0;

        // ---- Lifetime ----

        // The service owns Persistent NativeArrays, NativeLists and
        // GraphicsBuffers; dropping the reference without disposing leaks them
        // for the lifetime of the process (and, in the editor, across every
        // domain reload). Subscribed once, the first time the service exists.
        private static void InstallLifetimeHooks()
        {
            if (lifetimeHooksInstalled) return;
            lifetimeHooksInstalled = true;
            Application.quitting += DisposeService;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeService;
#endif
        }

        private static void DisposeService()
        {
            if (service == null) return;
            service.DisposeAll();
            service = null;
            DisposeCountForTests++;
        }

        // ---- Test introspection ----

        internal static void ResetForTests() => DisposeService();

        /// <summary>
        /// How many times the service's buffers have been released. Lets a
        /// test prove that tearing the service down actually disposed it
        /// rather than dropping the reference.
        /// </summary>
        internal static int DisposeCountForTests { get; private set; }

        internal static int GetInstanceCountForTests(int rendererKey) => Service.GetInstanceCount(rendererKey);
        internal static Matrix4x4 GetMatrixForTests(int rendererKey, int slot) => Service.GetMatrix(rendererKey, slot);
        internal static float GetCullDistanceForTests(int rendererKey) => Service.GetCullDistance(rendererKey);
        internal static ProfileSnapshot GetSettingsForTests(int rendererKey) => Service.GetSettings(rendererKey);
        internal static TreeRendererService.LastFrameStats GetLastFrameStatsForTests() => Service.Stats;
        internal static int FadeMaterialCountForTests => Service.FadeMaterialCount;
        internal static bool OcclusionCullingSupportedForTests => OcclusionCulling.Supported;

        // Deterministic per-test render: invokes the cull/LOD/submit path
        // synchronously for the given camera, independent of Unity's render
        // lifecycle hooks. Animated cross-fades advance by deltaTime.
        internal static void TickForTests(Camera camera) => Service.RenderFrame(camera, 0f);
        internal static void TickForTests(Camera camera, float deltaTime) => Service.RenderFrame(camera, deltaTime);
    }
}
