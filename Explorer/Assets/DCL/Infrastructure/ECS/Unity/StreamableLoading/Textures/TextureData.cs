using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using System;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public class TextureData : StreamableRefCountData<AnyTexture>, ISizedContent
    {
        public TextureData(AnyTexture asset, string reportCategory = ReportCategory.STREAMABLE_LOADING) : base(asset, reportCategory) { }

        public long ByteSize => Asset.ByteSize;

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.TexturesAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.TexturesReferenced;

        protected override void DestroyObject() =>
            Asset.DestroyObject();

        /// <summary>
        ///     Throws an exception or returns the Texture2D if the underlying asset is not a video texture
        /// </summary>
        public Texture2D EnsureTexture2D() =>
            Asset.Match(_ => throw new ArgumentException("Expected Texture2D, got VideoTexture"), texture => texture);

        public static implicit operator Texture?(TextureData? textureData) =>
            textureData?.Asset.Texture;

        public override string ToString() =>
            $"{nameof(TextureData)} {Asset.Width}x{Asset.Height}";
    }
}
