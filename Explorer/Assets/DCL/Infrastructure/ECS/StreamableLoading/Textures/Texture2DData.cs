﻿using DCL.Diagnostics;
using DCL.Profiling;
using DCL.WebRequests;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public class Texture2DData : StreamableRefCountData<Texture2D>, ISizedContent
    {
        private readonly IOwnedTexture2D? ownedTexture2D;
        public string? VideoURL { get; set; }

        public long ByteSize => Asset.GetRawTextureData<byte>().Length;

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.TexturesAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.TexturesReferenced;

        protected override void DestroyObject()
        {
            ownedTexture2D?.Dispose();

            if (VideoURL == null)
                UnityObjectUtils.SafeDestroy(Asset);
        }

        public Texture hack;

        public Texture2DData(Texture texture) : base(null, ReportCategory.TEXTURES)
        {
            hack = texture;
        }

        public Texture2DData(Texture2D texture) : base(texture, ReportCategory.TEXTURES) { }

        public Texture2DData(Texture2D texture, string videoUrl) : base(texture, ReportCategory.TEXTURES)
        {
            VideoURL = videoUrl;
        }

        public Texture2DData(IOwnedTexture2D asset) : base(asset.Texture, ReportCategory.TEXTURES)
        {
            ownedTexture2D = asset;
        }

        public override string ToString() =>
            $"{nameof(Texture2DData)} {Asset.name} {Asset.width}x{Asset.height}";
    }
}
