using ECS.StreamableLoading.AssetBundles;
using UnityEngine;

namespace ECS.StreamableLoading
{
    public interface IAssetData
    {
        public GameObject MainAsset { get; }
        public AssetBundleData? BundleData => null;
        public AnimationClip[]? AnimationClips => null;
    }
}
