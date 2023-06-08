using Arch.Core;
using Arch.SystemGroups;
using ECS.StreamableLoading.Common.Systems;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.AssetBundles
{
    [UpdateInGroup(typeof(StreamableLoadingGroup))]
    [UpdateAfter(typeof(ConcludeAssetBundleLoadingSystem))]
    public partial class RepeatAssetBundleLoadingSystem : RepeatLoadingSystemBase<GetAssetBundleIntention, AssetBundle>
    {
        internal RepeatAssetBundleLoadingSystem(World world) : base(world) { }

        protected override UnityWebRequest CreateWebRequest(in GetAssetBundleIntention intention) =>
            StartLoadingAssetBundleSystem.GetAssetBundleRequest(in intention);
    }
}
