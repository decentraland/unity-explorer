using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Profiling;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontData : StreamableRefCountData<FontFamilyAssets>
    {
        private readonly FontFileStore.Lease?[] files;

        public FontData(FontFamilyAssets assets, params FontFileStore.Lease?[] files) : base(assets, ReportCategory.SDK_FONTS)
        {
            this.files = files;
            ProfilingCounters.FontsAmount.Value++;
        }

        protected override ref ProfilerCounterValue<int> totalCount => ref ProfilingCounters.FontsAmount;

        protected override ref ProfilerCounterValue<int> referencedCount => ref ProfilingCounters.FontsReferenced;

        protected override void DestroyObject()
        {
            Asset.Destroy();
            FontFileStore.ReleaseAfterDestructionAsync(files).Forget(e => ReportHub.LogException(e, ReportCategory.SDK_FONTS));
        }
    }
}
