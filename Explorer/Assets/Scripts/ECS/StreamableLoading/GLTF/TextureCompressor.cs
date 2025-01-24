using UnityEngine;

namespace ECS.StreamableLoading.GLTF
{
    public class TextureCompressor
    {
        public Texture2D CompressToTargetFormat(Texture2D sourceTexture, TextureFormat targetFormat)
        {
            // Create an intermediate readable texture with RGBA32 format
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

                // Get raw pixel data
                byte[] rawTextureData = intermediateTexture.GetRawTextureData();

                // Create the final compressed texture
                Texture2D compressedTexture = new Texture2D(
                    sourceTexture.width,
                    sourceTexture.height,
                    targetFormat,
                    false);

                // Load raw data and force compression
                compressedTexture.LoadRawTextureData(rawTextureData);
                compressedTexture.Compress(highQuality: true);
                compressedTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

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

        /*public Texture2D CompressToTargetFormat(Texture2D sourceTexture, TextureFormat targetFormat)
        {
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

               // Create a new texture with the same dimensions and format as the source
               Texture2D tempTexture = new Texture2D(
                   sourceTexture.width,
                   sourceTexture.height,
                   TextureFormat.RGBA32,
                   false);

               // Read pixels from the RenderTexture into the temporary texture
               tempTexture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
               tempTexture.Apply();

               // Encode the texture to a format that can be loaded into a BC7 texture
               byte[] encodedData = tempTexture.EncodeToPNG(); // Use PNG to preserve color data
               Object.Destroy(tempTexture);

               // Create the final compressed texture
               Texture2D compressedTexture = new Texture2D(
                   sourceTexture.width,
                   sourceTexture.height,
                   targetFormat,
                   false);

               // Load the encoded data into the compressed texture
               compressedTexture.LoadImage(encodedData, false);

               // Restore previous RenderTexture
               RenderTexture.active = previous;

               return compressedTexture;
           }
           finally
           {
               // Cleanup
               RenderTexture.ReleaseTemporary(rt);
           }
        }*/
    }
}
