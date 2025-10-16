using UnityEngine;

namespace DCL.Utilities.Extensions
{
    public static class TextureExtensions
    {
        public static bool HasEqualResolution(this Texture2D texture, Texture to) =>
            texture.width == to.width && texture.height == to.height;

        public static bool HasEqualResolution(this RenderTexture texture, Texture to) =>
            texture.width == to.width && texture.height == to.height;

        public static void ResizeTexture(this Texture2D videoTexture, Texture to)
        {
            videoTexture.Reinitialize(to.width, to.height);
            videoTexture.Apply();
        }

        public static void ResizeTexture(this RenderTexture videoTexture, Texture targetTexture)
        {
            videoTexture.Release();
            videoTexture.width = targetTexture.width;
            videoTexture.height = targetTexture.height;
            videoTexture.Create();
        }
    }
}
