using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class TextureCompressor
    {
        public Texture2D CompressToTargetFormat(Texture2D sourceTexture, TextureFormat targetFormat)
        {
            // Create an intermediate readable texture with uncompressed format
            var intermediateTexture = new Texture2D(
                sourceTexture.width,
                sourceTexture.height,
                TextureFormat.RGBA32,
                false);

            // Create a temporary RenderTexture
            RenderTexture rt = RenderTexture.GetTemporary(
                sourceTexture.width,
                sourceTexture.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);

            try
            {
                // Copy source texture to the render texture
                Graphics.Blit(sourceTexture, rt);

                // Store current active RenderTexture
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = rt;

                // Read pixels to the intermediate texture
                intermediateTexture.ReadPixels(
                    new Rect(0, 0, rt.width, rt.height),
                    0, 0);
                intermediateTexture.Apply();

                // Encode to PNG to get proper compressed data
                byte[] pngData = intermediateTexture.EncodeToPNG();

                // Create the final compressed texture
                Texture2D compressedTexture = new Texture2D(
                    sourceTexture.width,
                    sourceTexture.height,
                    targetFormat,
                    false);

                // Load the PNG data which will automatically handle the compression
                compressedTexture.LoadImage(pngData, false);

                // Restore previous RenderTexture
                RenderTexture.active = previous;

                // Clean up intermediate texture
                Object.Destroy(intermediateTexture);

                return compressedTexture;
            }
            finally
            {
                // Cleanup
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
