using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class TextureUtilities
{
    public static GraphicsFormat GetColorSpaceFormat()
    {
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
            return GraphicsFormat.A2R10G10B10_UNormPack32;

        return GraphicsFormat.R32G32B32A32_SFloat;
    }

    public static Texture2D EnsureRGBA32Format(Texture2D sourceTexture)
    {
        if (sourceTexture.format == TextureFormat.RGBA32)
            return sourceTexture;

        Texture2D rgba32Texture = new Texture2D(
            sourceTexture.width,
            sourceTexture.height,
            TextureFormat.RGBA32,
            false,
            false);

        if (sourceTexture.isReadable)
        {
            rgba32Texture.SetPixels(sourceTexture.GetPixels());
            rgba32Texture.Apply();
            return rgba32Texture;
        }

        // For non-readable textures, use RenderTexture approach
        RenderTexture rt = RenderTexture.GetTemporary(
            sourceTexture.width,
            sourceTexture.height,
            0,
            RenderTextureFormat.ARGB32);

        try
        {
            Graphics.Blit(sourceTexture, rt);

            // Borrow active RT
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            rgba32Texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            rgba32Texture.Apply();

            // Return previously active RT
            RenderTexture.active = previous;

            return rgba32Texture;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
        }
    }
}
