using System;
using UnityEngine;

namespace ECS.Unity.Textures.Components
{
    public readonly struct TextureComponent : IEquatable<TextureComponent>
    {
        public readonly string Src;
        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly bool IsVideoTexture;
        public readonly int VideoPlayerEntity;
        public readonly Vector2 TextureOffset;
        public readonly Vector2 TextureTiling;
        public readonly string FileHash;

        private string cacheKey => string.IsNullOrEmpty(FileHash) ? Src : FileHash;

        public TextureComponent(
            string src,
            string fileHash,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp,
            FilterMode filterMode = FilterMode.Bilinear,
            bool isVideoTexture = false,
            int videoPlayerEntity = 0,
            Vector2 textureOffset = default,
            Vector2 textureTiling = default)
        {
            Src = src;
            FileHash = fileHash;
            WrapMode = wrapMode;
            FilterMode = filterMode;
            IsVideoTexture = isVideoTexture;
            VideoPlayerEntity = videoPlayerEntity;
            TextureOffset = textureOffset;
            TextureTiling = textureTiling;
        }

        public bool Equals(TextureComponent other) =>
            cacheKey == other.cacheKey &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            IsVideoTexture == other.IsVideoTexture &&
            VideoPlayerEntity == other.VideoPlayerEntity &&
            TextureOffset == other.TextureOffset &&
            TextureTiling == other.TextureTiling;

        public override bool Equals(object obj) =>
            obj is TextureComponent other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(cacheKey, (int)WrapMode, (int)FilterMode, IsVideoTexture, VideoPlayerEntity, TextureOffset, TextureTiling);
    }
}
