using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Common.Systems;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    public partial class StartLoadingAssetBundleSystem : StartLoadingSystemBase<GetAssetBundleIntention>
    {
        internal StartLoadingAssetBundleSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetAssetBundleIntention intention) =>
            GetAssetBundleRequest(intention);

        public static UnityWebRequest GetAssetBundleRequest(in GetAssetBundleIntention intention) =>
            intention.cacheHash.HasValue
                ? UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL, intention.cacheHash.Value)
                : UnityWebRequestAssetBundle.GetAssetBundle(intention.CommonArguments.URL);
    }
}
