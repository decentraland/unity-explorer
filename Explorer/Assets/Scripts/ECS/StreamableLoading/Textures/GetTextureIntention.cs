using CRDT;
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
        public readonly bool IsVideoTexture;
        public readonly CRDTEntity VideoPlayerEntity;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public GetTextureIntention(string url, TextureWrapMode wrapMode, FilterMode filterMode, int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            CommonArguments = new CommonLoadingArguments(url, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            IsVideoTexture = false;
            VideoPlayerEntity = -1;
        }

        public GetTextureIntention(CRDTEntity videoPlayerEntity)
        {
            CommonArguments = new CommonLoadingArguments(string.Empty);
            WrapMode = TextureWrapMode.Clamp;
            FilterMode = FilterMode.Bilinear;
            IsVideoTexture = true;
            VideoPlayerEntity = videoPlayerEntity;
        }

        public bool Equals(GetTextureIntention other) =>
            this.AreUrlEquals(other) &&
            WrapMode == other.WrapMode &&
            FilterMode == other.FilterMode &&
            IsVideoTexture == other.IsVideoTexture &&
            VideoPlayerEntity.Equals(other.VideoPlayerEntity);

        public override bool Equals(object obj) =>
            obj is GetTextureIntention other && Equals(other);

        public override readonly int GetHashCode() =>
            HashCode.Combine((int)WrapMode, (int)FilterMode, CommonArguments.URL, IsVideoTexture, VideoPlayerEntity);

        public override readonly string ToString() =>
            $"Get Texture: {(IsVideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";
    }
}
