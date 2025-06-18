using CRDT;
using DCL.WebRequests;
using Decentraland.Common;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Textures.Components;
using System;
using System.Threading;
using UnityEngine;
using TextureWrapMode = UnityEngine.TextureWrapMode;

namespace ECS.StreamableLoading.Textures
{
    public struct GetTextureIntention : ILoadingIntention, IEquatable<GetTextureIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly TextureType TextureType;
        public readonly CRDTEntity VideoPlayerEntity;
        public readonly string FileHash;

        public readonly string? AvatarId;

        public readonly TextureSource Src;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        // Note: Depending on the origin of the texture, it may not have a file hash, so the source URL is used in equality comparisons
        private readonly string cacheKey => string.IsNullOrEmpty(FileHash) ? CommonArguments.URL.OriginalString : FileHash;

        public GetTextureIntention(TextureSource textureSource, string fileHash, TextureWrapMode wrapMode, FilterMode filterMode, TextureType textureType,
            int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            Src = textureSource;
            CommonArguments = new CommonLoadingArguments(textureSource.TextureType == TextureUnion.TexOneofCase.Texture ? textureSource.GetUri() : null!, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
            VideoPlayerEntity = -1;
            FileHash = fileHash;
            AvatarId = null;
        }

        public GetTextureIntention(CRDTEntity videoPlayerEntity)
        {
            CommonArguments = new CommonLoadingArguments(null!);
            Src = TextureSource.CreateVideoTexture();
            FileHash = string.Empty;
            WrapMode = TextureWrapMode.Clamp;
            FilterMode = FilterMode.Bilinear;
            VideoPlayerEntity = videoPlayerEntity;
            TextureType = TextureType.Albedo; //Ignored
            AvatarId = null;
        }

        public bool Equals(GetTextureIntention other) =>
            cacheKey == other.cacheKey &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            Src.TextureType == other.Src.TextureType &&
            VideoPlayerEntity.Equals(other.VideoPlayerEntity);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public readonly override int GetHashCode() =>
            HashCode.Combine((int)WrapMode, (int)FilterMode, cacheKey, Src, VideoPlayerEntity);

        public readonly override string ToString() =>
            $"Get Texture: {(Src.TextureType == TextureUnion.TexOneofCase.VideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";

        public class DiskHashCompute : AbstractDiskHashCompute<GetTextureIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetTextureIntention asset)
            {
                keyPayload.Put(asset.cacheKey);
                keyPayload.Put((int)asset.WrapMode);
                keyPayload.Put((int)asset.FilterMode);
                keyPayload.Put((int)asset.Src.TextureType);
                keyPayload.Put(asset.VideoPlayerEntity.Id);
            }
        }
    }
}
