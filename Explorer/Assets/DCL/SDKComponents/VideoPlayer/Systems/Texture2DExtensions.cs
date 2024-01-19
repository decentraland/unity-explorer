using UnityEngine;

namespace DCL.SDKComponents.VideoPlayer.Systems
{
    public static class Texture2DExtensions
    {
        public static bool HasEqualResolution(this Texture2D texture, Texture to) =>
            texture.width == to.width && texture.height == to.height;
    }
}
