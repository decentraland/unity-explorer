using AOT;
using DCL.BevyMigration.Bridge.Generated;
using DCL.Diagnostics;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     Lifecycle of the native bridge (BevyMigration/docs/README.md § 8): init and shutdown of the Bevy thread,
    ///     the shared output textures, and the log callback. Re-entrant on purpose: the Editor never unloads native
    ///     plugins, so a domain reload shuts the bridge down before the reload and initializes it again after.
    /// </summary>
    public static unsafe class BevyBridge
    {
        public const int OUTPUT_BUFFER_COUNT = 2;

        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);
        private static readonly Texture2D?[] OUTPUTS = new Texture2D?[OUTPUT_BUFFER_COUNT];
        // Kept alive for the process lifetime: the native side holds the function pointer.
        private static readonly NativeLogCallback LOG_CALLBACK = OnNativeLog;
        private static readonly IntPtr LOG_CALLBACK_POINTER = Marshal.GetFunctionPointerForDelegate(LOG_CALLBACK);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeLogCallback(BmLogLevel level, byte* message);

        private static bool initialized;
        private static ulong lastCompletedFrame;

        public static bool IsInitialized => initialized;

        public static ulong LastCompletedFrame => lastCompletedFrame;

        public static int Width { get; private set; }

        public static int Height { get; private set; }

        /// <summary>
        ///     Starts the bridge with IOSurface-shared outputs of the given size. Returns false and logs when the
        ///     native library refuses (unsupported platform, no GPU, ABI mismatch).
        /// </summary>
        public static bool Initialize(int width, int height)
        {
            if (initialized) Shutdown();

            uint abi = NativeMethods.bm_abi_version();

            if (abi != BridgeAbi.VERSION)
            {
                ReportHub.LogError(REPORT_DATA, $"Bevy bridge ABI mismatch: library {abi}, bindings {BridgeAbi.VERSION}; rebuild BevyMigration/Engine");
                return false;
            }

            var config = new BmInitConfig
            {
                abi_version = BridgeAbi.VERSION,
                width = (uint)width,
                height = (uint)height,
                output = BmOutputKind.IoSurface,
                log = (delegate* unmanaged[Cdecl]<BmLogLevel, byte*, void>)LOG_CALLBACK_POINTER,
            };

            BmResult result = NativeMethods.bm_init(&config);

            if (result != BmResult.Ok)
            {
                ReportHub.LogError(REPORT_DATA, $"Bevy bridge init failed: {result}");
                return false;
            }

            for (uint i = 0; i < OUTPUT_BUFFER_COUNT; i++)
            {
                BmOutputTexture output;
                result = NativeMethods.bm_get_output(i, &output);

                if (result != BmResult.Ok || output.host_texture == null)
                {
                    ReportHub.LogError(REPORT_DATA, $"Bevy bridge output {i} unavailable: {result}");
                    NativeMethods.bm_shutdown();
                    return false;
                }

                var texture = Texture2D.CreateExternalTexture((int)output.width, (int)output.height, TextureFormat.BGRA32, false, false, (IntPtr)output.host_texture);
                texture.name = $"BevyOutput{i}";
                texture.hideFlags = HideFlags.DontSave;
                OUTPUTS[i] = texture;
            }

            Width = width;
            Height = height;
            lastCompletedFrame = 0;
            initialized = true;
            RegisterTeardown();
            ReportHub.Log(REPORT_DATA, $"Bevy bridge initialized at {width}x{height}");
            return true;
        }

        public static void Shutdown()
        {
            if (!initialized) return;

            initialized = false;
            NativeMethods.bm_shutdown();

            for (var i = 0; i < OUTPUT_BUFFER_COUNT; i++)
            {
                Texture2D? texture = OUTPUTS[i];
                OUTPUTS[i] = null;

                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }

            ReportHub.Log(REPORT_DATA, "Bevy bridge shut down");
        }

        /// <summary>
        ///     Polls the native side; returns the output texture holding the latest completed frame, or null while
        ///     nothing has completed yet.
        /// </summary>
        public static Texture2D? BeginFrame(out BmFrameStatus status)
        {
            status = default(BmFrameStatus);
            if (!initialized) return null;

            fixed (BmFrameStatus* ptr = &status)
            {
                if (NativeMethods.bm_begin_frame(ptr) != BmResult.Ok)
                    return null;
            }

            if (status.completed_frame == 0) return null;

            lastCompletedFrame = status.completed_frame;
            return OUTPUTS[(int)(status.completed_buffer % OUTPUT_BUFFER_COUNT)];
        }

        public static BmResult Submit(ref BmSnapshot snapshot)
        {
            if (!initialized) return BmResult.NotInitialized;

            fixed (BmSnapshot* ptr = &snapshot)
                return NativeMethods.bm_submit_snapshot(ptr);
        }

        /// <summary>
        ///     Queues one resource command; the native side copies the payload before returning.
        /// </summary>
        public static BmResult Resource(ref BmResourceCmd command)
        {
            if (!initialized) return BmResult.NotInitialized;

            fixed (BmResourceCmd* ptr = &command)
                return NativeMethods.bm_resource_cmd(ptr);
        }

        public static BmResult UploadMaterial(uint handle, ref BmMaterial material)
        {
            if (!initialized) return BmResult.NotInitialized;

            fixed (BmMaterial* ptr = &material)
                return NativeMethods.bm_upload_material(handle, ptr);
        }

        public static IntPtr RenderEventFunction =>
            initialized ? (IntPtr)NativeMethods.bm_get_render_event_func() : IntPtr.Zero;

        private static void RegisterTeardown()
        {
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                Shutdown();
        }
#endif

        [MonoPInvokeCallback(typeof(NativeLogCallback))]
        private static void OnNativeLog(BmLogLevel level, byte* message)
        {
            string text = Marshal.PtrToStringUTF8((IntPtr)message) ?? string.Empty;

            switch (level)
            {
                case BmLogLevel.Error:
                    ReportHub.LogError(REPORT_DATA, $"[bevy] {text}");
                    break;
                case BmLogLevel.Warning:
                    ReportHub.LogWarning(REPORT_DATA, $"[bevy] {text}");
                    break;
                default:
                    ReportHub.Log(REPORT_DATA, $"[bevy] {text}");
                    break;
            }
        }
    }

    /// <summary>
    ///     Mirrors `abi::ABI_VERSION`; csbindgen does not emit constants, so this is the hand-kept half of the check.
    /// </summary>
    public static class BridgeAbi
    {
        public const uint VERSION = 2;
    }
}
