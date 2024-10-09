using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public class TexturesUnzip : ITexturesUnzip
    {
        public TexturesUnzip()
        {
            bool result = NativeMethods.TexturesFuseInitialize();

            if (result == false)
                throw new Exception("TexturesFuseInitialize failed");
        }

        ~TexturesUnzip()
        {
            bool result = NativeMethods.TexturesFuseDispose();

            if (result == false)
                throw new Exception("TexturesFuseDispose failed");
        }

        public OwnedTexture2D TextureFromBytes(ReadOnlySpan<byte> bytes)
        {
            var texture = NewImage(bytes, out IntPtr handle);
            return new OwnedTexture2D(texture, handle);
        }

        private static Texture2D NewImage(ReadOnlySpan<byte> bytes, out IntPtr handle)
        {
            unsafe
            {
                fixed (byte* ptr = bytes)
                {
                    handle = NativeMethods.TexturesFuseProcessedImageFromMemory(
                        ptr,
                        bytes.Length,
                        out byte* output,
                        out int outputLength
                    );

                    if (handle == IntPtr.Zero)
                        throw new Exception("TexturesFuseProcessedImageFromMemory failed");

                    //TODO obtain size and formats
                    var texture = new Texture2D(10, 10, GraphicsFormat.R8_SInt, 10, TextureCreationFlags.Crunch);

                    // Texture2D.CreateExternalTexture()
                    texture.LoadRawTextureData(new IntPtr(output), outputLength);

                    texture.Apply();

                    return texture;
                }
            }
        }
    }
}
