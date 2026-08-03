using System;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    // Whether scenes may open plaintext http:// media, allowed by default to
    // match historical behavior. The native protocol whitelist keeps http as a
    // static baseline; this is the actual control point, checked per media open
    // because the native runtime initializes before feature flags resolve.
    public static class MediaProtocolPolicy
    {
        private static bool installed;

        public static bool AllowPlaintextHttp { get; private set; } = true;

        public static void Install(bool allowPlaintextHttp)
        {
            if (installed)
                throw new InvalidOperationException($"{nameof(MediaProtocolPolicy)} is already installed");

            installed = true;
            AllowPlaintextHttp = allowPlaintextHttp;
        }

        // statics survive between play sessions when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            installed = false;
            AllowPlaintextHttp = true;
        }
    }
}
