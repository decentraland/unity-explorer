using REnum;
using UnityEngine;
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
        public long ByteSize => Match(static _ => 0L, tex2d => tex2d.GetRawTextureData<byte>().Length);

        public Texture Texture => Match<Texture>(static video => video.Texture, static tex2d => tex2d);

        public int Width => Match(static video => video.Texture.width, static tex2d => tex2d.width);

        public int Height => Match(static video => video.Texture.height, static tex2d => tex2d.height);

        internal void DestroyObject() =>
            Match(static video => video.Dispose(), static tex2d => UnityObjectUtils.SafeDestroy(tex2d));

        public static implicit operator AnyTexture(Texture2D texture2D) =>
            FromTexture2D(texture2D);
    }
}
