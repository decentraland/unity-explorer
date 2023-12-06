using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;

namespace ECS.StreamableLoading.AssetBundles
{
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PrepareGlobalAssetBundleLoadingParametersSystem))]
    public partial class ReportGlobalAssetBundleErrorSystem : ReportAssetBundleErrorSystem
    {
        public ReportGlobalAssetBundleErrorSystem(World world, IReportsHandlingSettings settings) : base(world, settings) { }
    }
}
