using ECS.StreamableLoading.Common.Components;
using System.Collections.Generic;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class StaticSceneAssetBundle
    {
        public bool Supported;
        public bool Request;

        public StreamableLoadingResult<AssetBundleData> AssetBundleData = new ();
        public AssetBundlePromise AssetBundlePromise = AssetBundlePromise.NULL;
        public Dictionary<string, GameObject> Assets;

        public void RequestAssetBundle()
        {
            Request = true;
        }


    }
}
