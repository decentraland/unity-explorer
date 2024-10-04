using DCL.Diagnostics;
using DCL.Profiling;
using Unity.Profiling;
using UnityEngine;
using Utility;

namespace ECS.StreamableLoading.Textures
{
    public class Texture2DData : StreamableRefCountData<Texture2D>
    {
        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.TexturesAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.TexturesReferenced;

        protected override void DestroyObject()
        {
            UnityObjectUtils.SafeDestroy(Asset);
        }

        public Texture2DData(Texture2D asset) : base(asset, ReportCategory.TEXTURES) { }
    }
}
