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
        public readonly string FileHash;

        public TextureComponent(string src, string fileHash, TextureWrapMode wrapMode = TextureWrapMode.Clamp, FilterMode filterMode = FilterMode.Bilinear, bool isVideoTexture = false, int videoPlayerEntity = 0)
        {
            Src = src;
            FileHash = fileHash;
            WrapMode = wrapMode;
            FilterMode = filterMode;
            IsVideoTexture = isVideoTexture;
            VideoPlayerEntity = videoPlayerEntity;
        }

        public bool Equals(TextureComponent other) =>
            FileHash == other.FileHash && WrapMode == other.WrapMode && FilterMode == other.FilterMode && IsVideoTexture == other.IsVideoTexture && VideoPlayerEntity == other.VideoPlayerEntity;

        public override bool Equals(object obj) =>
            obj is TextureComponent other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(FileHash, (int)WrapMode, (int)FilterMode, IsVideoTexture, VideoPlayerEntity);
    }
}
