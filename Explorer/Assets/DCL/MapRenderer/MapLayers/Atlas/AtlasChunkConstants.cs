using UnityEngine;

namespace DCL.MapRenderer.MapLayers.Atlas
{
    public static class AtlasChunkConstants
    {
        public static readonly Color FINAL_COLOR = Color.white;
        public static readonly Color INITIAL_COLOR = new (0, 0, 0, 0);

        public const float SATELLITE_SATURATION = 1f;
        public static readonly int SATURATION = Shader.PropertyToID("_Saturation");
    }
}