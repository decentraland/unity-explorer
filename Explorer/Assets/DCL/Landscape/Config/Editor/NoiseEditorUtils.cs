using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    public static class NoiseEditorUtils
    {
        public const float TEXTURE_MAX_SIZE = 450;
        public static readonly string[] TEXTURE_STRINGS = { "2x2", "GENESIS PLAZA", "FAR", "VERY FAR" };
        public static readonly int[] TEXTURE_RESOLUTIONS = { 32, 160, 512, 1024 };

        public static readonly int CS_RESULT = Shader.PropertyToID("ResultTexture");
        public static readonly int CS_NOISE_BUFFER = Shader.PropertyToID("NoiseBuffer");
        public static readonly int CS_WIDTH = Shader.PropertyToID("Width");
    }
}
