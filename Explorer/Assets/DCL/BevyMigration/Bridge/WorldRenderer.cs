using System;

namespace DCL.BevyMigration.Bridge
{
    /// <summary>
    ///     The `world=unity|bevy` flag (BevyMigration/docs/README.md § 5 "Flags, not reverts"): which engine draws
    ///     the world. Flips at runtime; everything that depends on it reads <see cref="Current" /> every frame.
    /// </summary>
    public enum WorldRenderer
    {
        Unity,
        Bevy,
    }

    public static class WorldRendererFlag
    {
        public const string UNITY = "unity";
        public const string BEVY = "bevy";

        public static WorldRenderer Current { get; private set; } = WorldRenderer.Unity;

        /// <summary>
        ///     Raised after <see cref="Current" /> changed, with the new value.
        /// </summary>
        public static event Action<WorldRenderer>? Changed;

        public static void Set(WorldRenderer renderer)
        {
            if (renderer == Current) return;

            Current = renderer;
            Changed?.Invoke(renderer);
        }

        public static bool TryParse(string? value, out WorldRenderer renderer)
        {
            switch (value)
            {
                case BEVY:
                    renderer = WorldRenderer.Bevy;
                    return true;
                case UNITY:
                    renderer = WorldRenderer.Unity;
                    return true;
                default:
                    renderer = WorldRenderer.Unity;
                    return false;
            }
        }
    }
}
