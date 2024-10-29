using DCL.Diagnostics;
using DCL.Profiling;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public class Texture2DData : StreamableRefCountData<Texture2D>
    {
        private readonly IOwnedTexture2D? ownedTexture2D;

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.TexturesAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.TexturesReferenced;

        protected override void DestroyObject()
        {
            ownedTexture2D?.Dispose();
            UnityObjectUtils.SafeDestroy(Asset);
        }

        public Texture2DData(Texture2D texture) : base(texture, ReportCategory.TEXTURES) { }

        public Texture2DData(IOwnedTexture2D asset) : base(asset.Texture, ReportCategory.TEXTURES)
        {
            ownedTexture2D = asset;
        }
    }
}
