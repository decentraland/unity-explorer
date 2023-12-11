using UnityEngine;

namespace DCL.Landscape.Config.Editor
{
    public static class NoiseEditorUtils
    {
        public const float TEXTURE_MAX_SIZE = 450;
        public static readonly string[] TEXTURE_STRINGS = { "256", "512", "1024", "2048" };
        public static readonly int[] TEXTURE_RESOLUTIONS = { 256, 512, 1024, 2048 };

        public static readonly int CS_RESULT = Shader.PropertyToID("ResultTexture");
        public static readonly int CS_NOISE_BUFFER = Shader.PropertyToID("NoiseBuffer");
        public static readonly int CS_WIDTH = Shader.PropertyToID("Width");
    }
}
