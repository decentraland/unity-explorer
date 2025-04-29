namespace VirtualTexture
{
    /// <summary>
    /// Defines scaling factors used throughout the virtual texturing system.
    /// Used to control the resolution of various textures and render targets.
    /// </summary>
    public enum ScaleFactor
    {
        // Original size (1:1 ratio)
        One,

        // Half size (1:2 ratio)
        Half,

        // Quarter size (1:4 ratio)
        Quarter,

        // Eighth size (1:8 ratio)
        Eighth,
    }

    /// <summary>
    /// Extension methods for the ScaleFactor enum to convert enumeration values to floating-point multipliers.
    /// </summary>
    public static class ScaleModeExtensions
    {
        /// <summary>
        /// Converts a ScaleFactor enum value to its corresponding floating point multiplier.
        /// </summary>
        /// <param name="mode">The ScaleFactor to convert</param>
        /// <returns>A float representing the scale multiplier (e.g., 0.5f for Half)</returns>
        public static float ToFloat(this ScaleFactor mode)
        {
            switch(mode)
            {
            case ScaleFactor.Eighth:
                return 0.125f;
            case ScaleFactor.Quarter:
                return 0.25f;
            case ScaleFactor.Half:
                return 0.5f;
            }
            return 1;
        }
    }
}