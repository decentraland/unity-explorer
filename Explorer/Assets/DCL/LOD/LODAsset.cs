using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using UnityEngine;
using Utility;

namespace DCL.LOD
{
    public struct LODAsset : IDisposable
    {
        public LODKey LodKey;
        public GameObject Root;
        public AssetBundleData AssetBundleReference;

        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            ProfilingCounters.LODAssetAmount.Value++;
        }

        public void Dispose()
        {
            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.LODAssetAmount.Value--;
        }
    }
}
