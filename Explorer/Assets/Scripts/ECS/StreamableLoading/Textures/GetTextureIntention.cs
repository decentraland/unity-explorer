using CRDT;
using ECS.StreamableLoading.Common.Components;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public struct GetTextureIntention : ILoadingIntention, IEquatable<GetTextureIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly TextureType TextureType;
        // OR
        public readonly bool IsVideoTexture;
        public readonly CRDTEntity VideoPlayerEntity;
        public readonly string FileHash;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        // Note: Depending on the origin of the texture, it may not have a file hash, so the source URL is used in equality comparisons
        private string cacheKey => string.IsNullOrEmpty(FileHash) ? CommonArguments.URL.Value : FileHash;

        public GetTextureIntention(string url, string fileHash, TextureWrapMode wrapMode, FilterMode filterMode, TextureType textureType, int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            CommonArguments = new CommonLoadingArguments(url, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
            IsVideoTexture = false;
            VideoPlayerEntity = -1;
            FileHash = fileHash;
        }

        public GetTextureIntention(CRDTEntity videoPlayerEntity)
        {
            CommonArguments = new CommonLoadingArguments(string.Empty);
            FileHash = string.Empty;
            WrapMode = TextureWrapMode.Clamp;
            FilterMode = FilterMode.Bilinear;
            IsVideoTexture = true;
            VideoPlayerEntity = videoPlayerEntity;
            TextureType = TextureType.Albedo; //Ignored
        }

        public bool Equals(GetTextureIntention other) =>
            this.AreUrlEquals(other) &&
            cacheKey == other.cacheKey &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            IsVideoTexture == other.IsVideoTexture &&
            VideoPlayerEntity.Equals(other.VideoPlayerEntity);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine((int)WrapMode, (int)FilterMode, CommonArguments.URL,cacheKey, IsVideoTexture, VideoPlayerEntity);

        public override readonly string ToString() =>
            $"Get Texture: {(IsVideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";
    }
}
