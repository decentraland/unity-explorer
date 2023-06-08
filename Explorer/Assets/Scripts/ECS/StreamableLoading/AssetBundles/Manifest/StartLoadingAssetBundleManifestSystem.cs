using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Common.Systems;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class StartLoadingAssetBundleManifestSystem : StartLoadingSystemBase<GetAssetBundleManifestIntention>
    {
        internal StartLoadingAssetBundleManifestSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetAssetBundleManifestIntention intention) =>
            UnityWebRequest.Get(intention.CommonArguments.URL);
    }
}
