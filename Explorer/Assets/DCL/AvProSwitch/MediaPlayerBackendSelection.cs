using System;
using UnityEngine;

namespace DCL.AvProSwitch
{
    // Chooses the backend every MediaPlayer instance attaches at Awake.
    // false = AVPro (the safe rollback default: an absent or failed feature
    // flag fetch keeps the battle-tested player). The composition root
    // installs the choice from the "use-custom-media-player" feature flag
    // before any media player is provisioned; installing twice within one
    // app run is a wiring error and throws.
    public static class MediaPlayerBackendSelection
    {
        private static bool installed;

        public static bool UseCustomPlayer { get; private set; }

        public static void Install(bool useCustomPlayer)
        {
            if (installed)
                throw new InvalidOperationException($"{nameof(MediaPlayerBackendSelection)} is already installed");

            installed = true;
            UseCustomPlayer = useCustomPlayer;
        }

        // Keeps the install-once guarantee scoped to a single app run when
        // domain reload is disabled in the editor (statics survive between
        // play sessions there).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForNewRun()
        {
            installed = false;
            UseCustomPlayer = false;
        }
    }
}
