using DCL.AssetsProvision;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using System;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace DCL.LOD
{
    public struct LODAsset : IDisposable
    {
        public LODKey LodKey;
        public GameObject Root;
        public AssetBundleData AssetBundleReference;
        public readonly bool LoadingFailed;
        private readonly ILODAssetsPool Pool;


        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODAssetsPool pool)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            Pool = pool;

            ProfilingCounters.LODAssetAmount.Value++;
        }

        public LODAsset(LODKey lodKey)
        {
            LodKey = lodKey;
            LoadingFailed = true;
            Root = null;
            AssetBundleReference = null;
            Pool = null;
        }

        public void Dispose()
        {
            if (LoadingFailed) return;

            AssetBundleReference.Dereference();
            AssetBundleReference = null;

            UnityObjectUtils.SafeDestroy(Root);

            ProfilingCounters.LODAssetAmount.Value--;
            ProfilingCounters.LODInstantiatedInCache.Value--;
        }

        public void EnableAsset()
        {
            if (LoadingFailed) return;

            ProfilingCounters.LODInstantiatedInCache.Value--;
            Root.SetActive(true);
            Root.transform.SetParent(null);

        }

        public void DisableAsset(Transform parentContainer)
        {
            if (LoadingFailed) return;

            ProfilingCounters.LODInstantiatedInCache.Value++;

            // This logic should not be executed if the application is quitting
            if (UnityObjectUtils.IsQuitting) return;

            Root.SetActive(false);
            Root.transform.SetParent(parentContainer);
        }

        public void Release()
        {
            if (!LoadingFailed)
                Pool.Release(LodKey, this);
        }
    }
}
