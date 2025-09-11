﻿using CRDT;
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
        public string ReportSource;

        public readonly TextureWrapMode WrapMode;
        public readonly FilterMode FilterMode;
        public readonly TextureType TextureType;

        // OR
        public readonly bool IsVideoTexture;
        public readonly CRDTEntity VideoPlayerEntity;
        public readonly string FileHash;
        public readonly string Src => CommonArguments.URL.Value;

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public readonly string? AvatarTextureUserId;

        public readonly bool IsAvatarTexture => !string.IsNullOrEmpty(AvatarTextureUserId);

        // Note: Depending on the origin of the texture, it may not have a file hash, so the source URL is used in equality comparisons
        private readonly string cacheKey => string.IsNullOrEmpty(FileHash) ? CommonArguments.URL.Value : FileHash;

        public GetTextureIntention(string url, string fileHash, TextureWrapMode wrapMode, FilterMode filterMode, TextureType textureType,
            string reportSource,
            int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            CommonArguments = new CommonLoadingArguments(url, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
            IsVideoTexture = false;
            VideoPlayerEntity = -1;
            FileHash = fileHash;
            AvatarTextureUserId = null;
            ReportSource = reportSource;
        }

        public GetTextureIntention(string userId,
            TextureWrapMode wrapMode, FilterMode filterMode, TextureType textureType,
            string reportSource,
            int attemptsCount = StreamableLoadingDefaults.ATTEMPTS_COUNT)
        {
            CommonArguments = new CommonLoadingArguments(string.Empty, attempts: attemptsCount);
            WrapMode = wrapMode;
            FilterMode = filterMode;
            TextureType = textureType;
            IsVideoTexture = false;
            VideoPlayerEntity = -1;
            FileHash = string.Empty;
            AvatarTextureUserId = userId;
            ReportSource = reportSource;
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
            AvatarTextureUserId = null;
            ReportSource = "Video Texture";
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
            $"Get Texture by {ReportSource}, {(IsAvatarTexture ? "isAvatarTexture" : string.Empty)} : {(IsVideoTexture ? $"Video {VideoPlayerEntity}" : CommonArguments.URL)}";

        public class DiskHashCompute : AbstractDiskHashCompute<GetTextureIntention>
        {
            /// <summary>
            /// Number added to the hash, to differentiate between incompatible serialize/deserialize types.
            /// E.g. after adding WrapMode and Filtering mode to meta data, previously downloaded textures could not be
            /// deserialized anymore.
            /// </summary>
            private const int ITERATION_NUMBER = 1;
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetTextureIntention asset)
            {
                keyPayload.Put(asset.cacheKey);
                keyPayload.Put(ITERATION_NUMBER);
                keyPayload.Put((int)asset.WrapMode);
                keyPayload.Put((int)asset.FilterMode);
                keyPayload.Put(asset.IsVideoTexture);
                keyPayload.Put(asset.VideoPlayerEntity.Id);
            }
        }
    }
}
