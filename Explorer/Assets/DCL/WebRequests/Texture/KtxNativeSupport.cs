using DCL.Diagnostics;
using KtxUnity;
using System;
using Unity.Collections;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Local capability check for the ktx_unity native decoder: machines where the OS cannot open the plugin
    ///     degrade to unconverted texture URLs. Main-thread only; a racing double probe is benign.
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
        ///     A runtime native load failure is per-machine-permanent: the capability stays off for the session.
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

                // On a healthy lib, garbage input makes Open return an error code without throwing; Dispose must only run after Open succeeds.
                ktxTexture.Open(probeBuffer.AsReadOnly());
                ktxTexture.Dispose();

                return true;
            }
            catch (Exception e)
            {
                // A broken native install can fail in shapes beyond DllNotFound; fail closed on any exception.
                ReportHub.LogWarning(ReportCategory.TEXTURE_WEB_REQUEST, $"ktx_unity probe failed ({e.GetType().Name}: {e.Message}); KTX2 conversion is disabled and textures fall back to unconverted URLs");
                return false;
            }
        }
    }
}
