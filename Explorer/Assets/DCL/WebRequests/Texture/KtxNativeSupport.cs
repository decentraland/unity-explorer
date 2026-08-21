using DCL.Diagnostics;
using KtxUnity;
using System;
using Unity.Collections;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Local capability check for the ktx_unity native decoder. KTX2 conversion is toggled by a remote feature
    ///     flag, so machines where the OS cannot open the native plugin (AV quarantine, missing VC++ runtime,
    ///     corrupted install) must degrade to unconverted texture URLs instead of failing every converted request
    ///     with <see cref="DllNotFoundException" />.
    ///     Not thread-safe: all reads and writes are expected on the main thread; a racing double probe is
    ///     benign (same cached result).
    /// </summary>
    public static class KtxNativeSupport
    {
        private const int PROBE_BUFFER_SIZE = 16;

        internal static Func<bool>? probeOverride;

        private static bool? isSupported;

        public static bool IsSupported
        {
            get
            {
                isSupported ??= Probe();
                return isSupported.Value;
            }
        }

        /// <summary>
        ///     A native load failure observed at runtime is per-machine-permanent: the capability stays off for the
        ///     rest of the session.
        /// </summary>
        internal static void MarkUnsupported()
        {
            isSupported = false;
        }

        internal static void Reset()
        {
            isSupported = null;
            probeOverride = null;
        }

        private static bool Probe()
        {
            try
            {
                if (probeOverride != null)
                    return probeOverride();

                using var probeBuffer = new NativeArray<byte>(PROBE_BUFFER_SIZE, Allocator.Temp);
                var ktxTexture = new KtxTexture();

                // Garbage input makes Open return an error code without throwing when the native library is
                // loadable, so the throw below is the only unsupported signal; Dispose must not run if Open threw
                // because native state only exists once Open returns. The error code is the expected healthy-lib
                // outcome, but the package logs it at Error level; logging is paused so the deliberate garbage
                // probe emits no error.
                UnityEngine.ILogger unityLogger = UnityEngine.Debug.unityLogger;
                bool logWasEnabled = unityLogger.logEnabled;
                unityLogger.logEnabled = false;

                try
                {
                    ktxTexture.Open(probeBuffer.AsReadOnly());
                    ktxTexture.Dispose();
                }
                finally { unityLogger.logEnabled = logWasEnabled; }

                return true;
            }
            catch (Exception e)
            {
                // A broken native install can fail in shapes beyond DllNotFound; the probe never throws and
                // fails closed instead, with the exception type named in the warning to keep it visible.
                ReportHub.LogWarning(ReportCategory.TEXTURE_WEB_REQUEST, $"ktx_unity probe failed ({e.GetType().Name}: {e.Message}); KTX2 conversion is disabled and textures fall back to unconverted URLs");
                return false;
            }
        }
    }
}
