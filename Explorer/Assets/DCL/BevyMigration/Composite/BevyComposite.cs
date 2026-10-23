using UnityEngine;

namespace DCL.BevyMigration.Composite
{
    /// <summary>
    ///     What the composite feature blits this frame: the output texture holding the last completed Bevy frame,
    ///     or null when Unity draws the world itself. Written by the bridge system, read by the render feature.
    /// </summary>
    public static class BevyComposite
    {
        public static Texture? Source { get; set; }

        /// <summary>
        ///     wgpu writes rows top-down while Unity reads a Texture2D bottom-up; the blit flips by default and the
        ///     debug panel can turn it off if a platform disagrees.
        /// </summary>
        public static bool FlipVertically { get; set; } = true;
    }
}
