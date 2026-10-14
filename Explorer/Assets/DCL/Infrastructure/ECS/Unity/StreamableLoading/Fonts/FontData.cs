using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontData : StreamableRefCountData<SceneFontAssets>
    {
        private readonly AssetBundleData bundle;

        // Owns one reference to the bundle the assets come from.
        public FontData(SceneFontAssets assets, AssetBundleData bundle) : base(assets, ReportCategory.SDK_FONTS)
        {
            this.bundle = bundle;
            ProfilingCounters.FontsAmount.Value++;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.FontsAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.FontsReferenced;

        protected override void DestroyObject()
        {
            Asset.Destroy();
            bundle.Dereference();
        }
    }
}
