using Arch.Core;
using Arch.SystemGroups;
using DCL.Diagnostics;
using ECS.StreamableLoading;
using ECS.StreamableLoading.Common.Systems;
using ECS.Unity.GLTFContainer.Asset.Components;

namespace ECS.Unity.GLTFContainer.Asset.Systems
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(CreateGltfAssetFromAssetBundleSystem))]
    [LogCategory(ReportCategory.GLTF_CONTAINER)]
    public partial class ReportGltfErrorsSystem : ReportStreamableLoadingErrorSystem<GetGltfContainerAssetIntention, GltfContainerAsset>
    {
        internal ReportGltfErrorsSystem(World world, IReportsHandlingSettings settings) : base(world, settings) { }
    }
}
