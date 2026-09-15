using DCL.Diagnostics;
using DCL.Profiling;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontData : StreamableRefCountData<FontFamilyAssets>
    {
        public FontData(FontFamilyAssets assets) : base(assets, ReportCategory.FONTS) { }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.FontsAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.FontsReferenced;

        protected override void DestroyObject()
        {
            Asset.Destroy();
        }
    }
}
