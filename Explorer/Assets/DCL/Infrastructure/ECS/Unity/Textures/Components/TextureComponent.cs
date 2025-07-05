using DCL.WebRequests;
using Decentraland.Common;
using System;
using UnityEngine;
using TextureWrapMode = UnityEngine.TextureWrapMode;
using Vector2 = UnityEngine.Vector2;

namespace ECS.Unity.Textures.Components
{
    public readonly struct TextureComponent : IEquatable<TextureComponent>
    {
        public readonly TextureSource Src;
        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly TextureType TextureType;
        public readonly bool IsVideoTexture;
        public readonly int VideoPlayerEntity;
        public readonly Vector2 TextureOffset;
        public readonly Vector2 TextureTiling;
        public readonly string FileHash;

        private string cacheKey => string.IsNullOrEmpty(FileHash) ? Src.Id : FileHash;

        public bool IsAvatarTexture => Src.TextureType == TextureUnion.TexOneofCase.AvatarTexture;

        public TextureComponent(
            TextureSource src,
            string fileHash,
            TextureWrapMode wrapMode = TextureWrapMode.Clamp,
            FilterMode filterMode = FilterMode.Bilinear,
            TextureType textureType = TextureType.Albedo,
            Vector2 textureOffset = default,
            Vector2 textureTiling = default,
            bool isVideoTexture = false,
            int videoPlayerEntity = 0)
        {
            Src = src;
            FileHash = fileHash;
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
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

        public TextureComponent WithTextureType(TextureType textureType) =>
            new (Src, FileHash, WrapMode, FilterMode, textureType, isVideoTexture: IsVideoTexture, videoPlayerEntity: VideoPlayerEntity);
    }
}
