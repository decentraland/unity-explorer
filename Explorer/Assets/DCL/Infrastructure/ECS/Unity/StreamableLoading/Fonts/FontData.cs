using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontData : StreamableRefCountData<FontFamilyAssets>
    {
        private readonly FontFileStore.Lease?[] files;
        private readonly AssetBundleData? bundle;

        public FontData(FontFamilyAssets assets, params FontFileStore.Lease?[] files) : base(assets, ReportCategory.SDK_FONTS)
        {
            this.files = files;
            ProfilingCounters.FontsAmount.Value++;
        }

        /// <param name="bundle">The converted font bundle the assets were loaded from; this font holds one reference to it.</param>
        public FontData(FontFamilyAssets assets, AssetBundleData bundle) : this(assets)
        {
            this.bundle = bundle;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.FontsAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.FontsReferenced;

        protected override void DestroyObject()
        {
            Asset.Destroy();
            bundle?.Dereference();
            FontFileStore.ReleaseAfterDestructionAsync(files).Forget(e => ReportHub.LogException(e, ReportCategory.SDK_FONTS));
        }
    }
}
