using REnum;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    /// <summary>
    ///     Represents any texture type, either Texture2D or VideoTexture.
    /// </summary>
    [REnum]
    [REnumField(typeof(VideoTextureData))]
    [REnumField(typeof(Texture2D))]
    public partial struct AnyTexture
    {
        public long ByteSize => Match(static _ => 0L, static tex2d => EstimateTextureByteSize(tex2d));

        private static long EstimateTextureByteSize(Texture2D tex2d)
        {
            // Destroyed textures (Unity fake-null) account as zero instead of throwing.
            if (tex2d == null) return 0L;
            if (tex2d.isReadable) return tex2d.GetRawTextureData<byte>().Length;

            // Non-readable textures have no CPU copy to measure (GetRawTextureData throws);
            // estimate the GPU footprint across the full mip chain instead.
            var size = (long)GraphicsFormatUtility.ComputeMipmapSize(tex2d.width, tex2d.height, tex2d.graphicsFormat);

            for (var mip = 1; mip < tex2d.mipmapCount; mip++)
                size += (long)GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, tex2d.width >> mip), Mathf.Max(1, tex2d.height >> mip), tex2d.graphicsFormat);

            return size;
        }

        public Texture Texture => Match<Texture>(static video => video.Texture, static tex2d => tex2d);

        public int Width => Match(static video => video.Texture.width, static tex2d => tex2d.width);

        public int Height => Match(static video => video.Texture.height, static tex2d => tex2d.height);

        internal void DestroyObject() =>
            Match(static video => video.Dispose(), static tex2d => UnityObjectUtils.SafeDestroy(tex2d));

        public static implicit operator AnyTexture(Texture2D texture2D) =>
            FromTexture2D(texture2D);
    }
}
