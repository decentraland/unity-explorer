using DCL.Diagnostics;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

public static class TextureUtilities
{
    public static GraphicsFormat GetColorSpaceFormat()
    {
        if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer)
        {
            // Option 1: Try to use high-precision linear (like Windows).
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32G32B32A32_SFloat, GraphicsFormatUsage.Render))
            {
                // Linear, 32-bit float per channel.
                return GraphicsFormat.R32G32B32A32_SFloat;
            }

            // Option 2: Fallback to a standard 8-bit sRGB format with good alpha.
            if (SystemInfo.IsFormatSupported(GraphicsFormat.B8G8R8A8_UNorm, GraphicsFormatUsage.Render))
            {
                // sRGB compatible, 8-bit per channel.
                return GraphicsFormat.B8G8R8A8_UNorm;
            }

            // Option 3: Fallback to an older 16-bit float format if the above aren't supported.
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, GraphicsFormatUsage.Render))
            {
                // Linear, 16-bit float per channel.
                return GraphicsFormat.R16G16B16A16_SFloat;
            }

            // Fallback to the original, but be aware of the 2-bit alpha limitation.
            return GraphicsFormat.A2R10G10B10_UNormPack32;
        }

        return GraphicsFormat.R32G32B32A32_SFloat;
    }

    /// <summary>
    /// Ensures a texture is in RGBA32 format and optionally resizes it if it exceeds the resolution cap.
    /// The resizing maintains the original aspect ratio by scaling the larger dimension to the cap and adjusting the other dimension proportionally.
    /// </summary>
    /// <param name="sourceTexture">The source texture to process</param>
    /// <param name="resolutionCap">Maximum allowed dimension (width or height). Defaults to 1024 (same cap used in Asset Bundles Converter).</param>
    /// <returns>A new texture in RGBA32 format with dimensions respecting the resolution cap</returns>
    public static Texture2D EnsureRGBA32Format(Texture2D sourceTexture, int resolutionCap = 1024)
    {
        int targetWidth = sourceTexture.width;
        int targetHeight = sourceTexture.height;

        if (targetWidth > resolutionCap || targetHeight > resolutionCap)
        {
            // Calculate new dimensions while maintaining aspect ratio
            float aspectRatio = (float)targetWidth / targetHeight;
            if (targetWidth > targetHeight)
            {
                targetWidth = resolutionCap;
                targetHeight = Mathf.RoundToInt(resolutionCap / aspectRatio);
            }
            else
            {
                targetHeight = resolutionCap;
                targetWidth = Mathf.RoundToInt(resolutionCap * aspectRatio);
            }
        }
        else if (sourceTexture.format == TextureFormat.RGBA32)
        {
            // If no resizing needed and format is already RGBA32, return original
            return sourceTexture;
        }

        Texture2D rgba32Texture = new Texture2D(
            targetWidth,
            targetHeight,
            TextureFormat.RGBA32,
            false,
            false);

        RenderTexture rt = RenderTexture.GetTemporary(
            targetWidth,
            targetHeight,
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
