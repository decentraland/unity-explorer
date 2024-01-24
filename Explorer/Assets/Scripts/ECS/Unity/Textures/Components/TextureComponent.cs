using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public readonly struct TextureComponent : IEquatable<TextureComponent>
    {
        public readonly string Src;
        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly bool VideoTexture;

        public TextureComponent(string src, TextureWrapMode wrapMode = TextureWrapMode.Clamp, FilterMode filterMode = FilterMode.Bilinear, bool videoTexture = false)
        {
            Src = src;
            WrapMode = wrapMode;
            FilterMode = filterMode;
            VideoTexture = videoTexture;
        }

        public bool Equals(TextureComponent other) =>
            Src == other.Src && WrapMode == other.WrapMode && FilterMode == other.FilterMode && VideoTexture == other.VideoTexture;

        public override bool Equals(object obj) =>
            obj is TextureComponent other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Src, (int)WrapMode, (int)FilterMode, VideoTexture);
    }
}
