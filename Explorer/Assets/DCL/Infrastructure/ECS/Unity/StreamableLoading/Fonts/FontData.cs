using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontData : StreamableRefCountData<SceneFontAssets>
    {
        private readonly AssetBundleData bundle;

        /// <param name="bundle">The converted font bundle the assets were loaded from; this font holds one reference to it.</param>
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
