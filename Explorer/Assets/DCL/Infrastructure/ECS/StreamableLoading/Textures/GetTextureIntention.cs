using CRDT;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
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
        public readonly bool IsAvatarTexture;
        public readonly CRDTEntity VideoPlayerEntity;
        public readonly string FileHash;
        public readonly string Src => CommonArguments.URL.Value;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        // Note: Depending on the origin of the texture, it may not have a file hash, so the source URL is used in equality comparisons
        private readonly string cacheKey => string.IsNullOrEmpty(FileHash) ? CommonArguments.URL.Value : FileHash;

        public GetTextureIntention(string url, string fileHash, TextureWrapMode wrapMode, FilterMode filterMode, TextureType textureType,
            int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT,
            bool isAvatarTexture = false)
        {
            CommonArguments = new CommonLoadingArguments(url, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
            IsVideoTexture = false;
            VideoPlayerEntity = -1;
            FileHash = fileHash;
            IsAvatarTexture = isAvatarTexture;
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
            IsAvatarTexture = false;
        }

        public bool Equals(GetTextureIntention other) =>
            cacheKey == other.cacheKey &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            IsVideoTexture == other.IsVideoTexture &&
            VideoPlayerEntity.Equals(other.VideoPlayerEntity);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine((int)WrapMode, (int)FilterMode, cacheKey, IsVideoTexture, VideoPlayerEntity);

        public readonly override string ToString() =>
            $"Get Texture: {(IsVideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";

        public class DiskHashCompute : AbstractDiskHashCompute<GetTextureIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetTextureIntention asset)
            {
                keyPayload.Put(asset.cacheKey);
                keyPayload.Put((int)asset.WrapMode);
                keyPayload.Put((int)asset.FilterMode);
                keyPayload.Put(asset.IsVideoTexture);
                keyPayload.Put(asset.VideoPlayerEntity.Id);
            }
        }
    }
}
