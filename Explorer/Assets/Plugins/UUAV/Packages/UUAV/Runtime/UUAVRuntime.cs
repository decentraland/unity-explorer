using System;
using System.Text;
using AOT;
using UnityEngine;

namespace UUAV
{
    /// <summary>
    /// Owns the native runtime lifecycle
    /// </summary>
    internal static class UUAVRuntime
    {
        private const string UNTRUSTED_STREAMING_PROTOCOLS = "https,http,crypto,data";

        private const UUAVLogLevel DefaultLogLevel = UUAVLogLevel.Warning;

        private static readonly ErrorCallback errorCallback = OnNativeError;
        private static readonly LogCallback warningCallback = OnNativeWarning;
        private static readonly LogCallback logCallback = OnNativeLog;

        private static IntPtr renderCallback;

        public static bool Initialized => NativeMethods.uuav_status().Initialized;

        public static IntPtr RenderCallback
        {
            get
            {
                if (renderCallback == IntPtr.Zero)
                {
                    renderCallback = NativeMethods.uuav_get_render_callback();
                }

                return renderCallback;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Init()
        {
            if (Initialized)
            {
                return;
            }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Metal)
            {
                Debug.LogError(
                    $"[UUAV] init: unsupported graphics API {SystemInfo.graphicsDeviceType}; macOS requires Metal"
                );
                return;
            }
#endif

            Application.quitting += Deinit;

            var config = AudioSettings.GetConfiguration();
            var audioOptions = AudioOptions.FromConfig(config);

            var protocols = UNTRUSTED_STREAMING_PROTOCOLS;
#if UNITY_EDITOR
            protocols += ",file,http";
#endif

            using var probe = ProbeTexture.New();
            var result = NativeMethods.uuav_init(probe.NativePtr(), audioOptions, errorCallback, warningCallback, logCallback, protocols, (int)DefaultLogLevel);

            if (result.IsOk == false)
            {
                Debug.LogError($"[UUAV] init: {result.ConsumeError()}");
                return;
            }

            UUAVFetchService.Register();


        }

        private static void Deinit()
        {
            if (Initialized == false)
            {
                return;
            }

            UUAVFetchService.Unregister();
            NativeMethods.uuav_deinit();
            renderCallback = IntPtr.Zero;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterDomainReloadTeardown()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= Deinit;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += Deinit;
        }
#endif


        [MonoPInvokeCallback(typeof(ErrorCallback))]
        private static void OnNativeError(IntPtr message)
        {
            Debug.LogError($"[UUAV] {Utf8.PtrToString(message)}");
        }

        [MonoPInvokeCallback(typeof(LogCallback))]
        private static void OnNativeWarning(IntPtr message)
        {
            Debug.LogWarning($"[UUAV] {Utf8.PtrToString(message)}");
        }

        [MonoPInvokeCallback(typeof(LogCallback))]
        private static void OnNativeLog(IntPtr message)
        {
            Debug.Log($"[UUAV] {Utf8.PtrToString(message)}");
        }

        public static void SetLogLevel(UUAVLogLevel level)
        {
            NativeMethods.uuav_set_log_level((int)level);
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("UUAV/Init")]
        public static void ManualInit()
        {
            Init();
        }

        [UnityEditor.MenuItem("UUAV/Deinit")]
        public static void ManualDeinit()
        {
            Deinit();
        }
        
        [UnityEditor.MenuItem("UUAV/Status")]
        public static void PrintStats()
        {
            Status status = NativeMethods.uuav_status();
            
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[UUAV] Status:");
            sb.Append("Initialized: ").AppendLine(status.Initialized.ToString());
            sb.Append("Players Count: ").AppendLine(status.PlayersCount.ToString());
            sb.Append("Audio Channels: ").AppendLine(status.AudioOptions.Channels.ToString());
            sb.Append("Audio SampleRate: ").AppendLine(status.AudioOptions.SampleRate.ToString());
            sb.Append("Device Reason: ").AppendLine(status.ConsumeDeviceRemoveReason());
            sb.AppendLine("[UUAV] :Status");

            Debug.Log(sb.ToString());
        }
#endif

        private readonly struct ProbeTexture : IDisposable
        {
            private readonly Texture2D probe;

            private ProbeTexture(Texture2D probe)
            {
                this.probe = probe;
            }

            public static ProbeTexture New()
            {
                var probe = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
                return new ProbeTexture(probe);
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(probe);
            }

            public IntPtr NativePtr()
            {
                return probe.GetNativeTexturePtr();
            }
        }
    }
}
