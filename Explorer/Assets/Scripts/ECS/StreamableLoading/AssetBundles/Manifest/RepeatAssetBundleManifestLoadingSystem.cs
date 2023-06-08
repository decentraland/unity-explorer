using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Common.Systems;
using SceneRunner.Scene;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles.Manifest
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(ConcludeAssetBundleManifestLoadingSystem))]
    public partial class RepeatAssetBundleManifestLoadingSystem : RepeatLoadingSystemBase<GetAssetBundleIntention, SceneAssetBundleManifest>
    {
        internal RepeatAssetBundleManifestLoadingSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetAssetBundleIntention intention) =>
            UnityWebRequest.Get(intention.CommonArguments.URL);
    }
}
