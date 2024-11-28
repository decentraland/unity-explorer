using System;
using UnityEngine;

namespace Plugins.TexturesFuse.TexturesServerWrap.Unzips
{
    public enum TextureType
    {
        Albedo,
        NormalMap,
    }

    public static class TextureTypeExtensions
    {
        public static NativeMethods.CMP_FORMAT AsBC_Format(this TextureType textureType) =>
            textureType switch
            {
                TextureType.Albedo => NativeMethods.CMP_FORMAT.CMP_FORMAT_BC7,
                TextureType.NormalMap => NativeMethods.CMP_FORMAT.CMP_FORMAT_BC5,
                _ => throw new ArgumentOutOfRangeException(nameof(textureType), textureType, null!),
            };

        public static TextureFormat AsBC_TextureFormat(this TextureType textureType) =>
            textureType switch
            {
                TextureType.Albedo => TextureFormat.BC7,
                TextureType.NormalMap => TextureFormat.BC5,
                _ => throw new ArgumentOutOfRangeException(nameof(textureType), textureType, null!),
            };
    }
}
