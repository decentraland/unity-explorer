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
                FullScreenMode.Windowed,
                FullScreenMode.FullScreenWindow,
                FullScreenMode.ExclusiveFullScreen
            };
    }
}
