using System.Collections.Generic;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles
{
    public class StaticSceneAssetBundle
    {
        public bool Supported;
        public bool ReadyToUse;
        public bool Request;
        public bool PromiseInitiated;



        public AssetBundleData AssetBundleData;
        public AssetBundlePromise AssetBundlePromise;
        public Dictionary<string, GameObject> Assets;
        public bool Consumed { get; set; }

        public void RequestAssetBundle()
        {
            Request = true;
        }


    }
}
