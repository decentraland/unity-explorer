using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.Textures.Components;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public struct GetTextureIntention : ILoadingIntention, IEquatable<GetTextureIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly bool IsReadable;
        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly bool IsVideoTexture;
        public readonly CRDTEntity VideoPlayerEntity;
        public readonly string FileHash;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public GetTextureIntention(string url, string fileHash, TextureWrapMode wrapMode, FilterMode filterMode, bool isReadable = false, int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            CommonArguments = new CommonLoadingArguments(url, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            IsReadable = isReadable;
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
            IsReadable = false;
            IsVideoTexture = true;
            VideoPlayerEntity = videoPlayerEntity;
        }

        public bool Equals(GetTextureIntention other) =>
            this.AreUrlEquals(other) &&
            FileHash == other.FileHash &&
            IsReadable == other.IsReadable &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            IsVideoTexture == other.IsVideoTexture &&
            VideoPlayerEntity.Equals(other.VideoPlayerEntity);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(IsReadable, (int)WrapMode, (int)FilterMode, CommonArguments.URL, FileHash, IsVideoTexture, VideoPlayerEntity);

        public override string ToString() =>
            $"Get Texture: {(IsVideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";
    }
}
