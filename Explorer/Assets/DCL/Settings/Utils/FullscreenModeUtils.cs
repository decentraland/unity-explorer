using Arch.LowLevel;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Settings.Utils
{
    public static class FullscreenModeUtils
    {
        public static readonly IReadOnlyList<FullScreenMode> Modes =
            new[]
            {
                FullScreenMode.Windowed, // Windowed
                FullScreenMode.FullScreenWindow, // Fullscreen Borderless
                FullScreenMode.ExclusiveFullScreen // Fullscreen
            };

        public static int IndexOf(FullScreenMode mode)
        {
            for (int i = 0; i < Modes.Count; i++)
                if (Modes[i] == mode) return i;

            return -1;
        }
    }
}
