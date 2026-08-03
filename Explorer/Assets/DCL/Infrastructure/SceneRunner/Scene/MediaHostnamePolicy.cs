using System;
using UnityEngine;

namespace SceneRunner.Scene
{
    // Whether a media url must resolve to a hostname the scene declared in
    // allowedMediaHostnames while holding ALLOW_MEDIA_HOSTNAMES. Defaults to
    // false - every deployed scene was built against that, so an absent or
    // failed feature flag fetch cannot black out media. Replaces the
    // CHECK_ALLOWED_MEDIA_HOSTNAMES compile symbol, defined in no build target:
    // https://github.com/decentraland/unity-renderer/pull/5844
    public static class MediaHostnamePolicy
    {
        private static bool installed;

        public static bool Enforced { get; private set; }

        public static void Install(bool enforced)
        {
            if (installed)
                throw new InvalidOperationException($"{nameof(MediaHostnamePolicy)} is already installed");

            installed = true;
            Enforced = enforced;
        }

        // statics survive between play sessions when domain reload is disabled
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            installed = false;
            Enforced = false;
        }
    }
}
