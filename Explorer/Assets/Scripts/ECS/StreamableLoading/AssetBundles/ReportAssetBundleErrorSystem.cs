using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.StreamableLoading.Common.Systems;

namespace ECS.StreamableLoading.AssetBundles
{
    [LogCategory(ReportCategory.ASSET_BUNDLES)]
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(PrepareAssetBundleLoadingParametersSystem))]
    public partial class ReportAssetBundleErrorSystem : ReportStreamableLoadingErrorSystem<GetAssetBundleIntention, AssetBundleData>
    {
        public ReportAssetBundleErrorSystem(World world, IReportsHandlingSettings settings) : base(world, settings) { }
    }
}
