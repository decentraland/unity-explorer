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
        private readonly bool LoadingFailed;

        private readonly LODDebugInfo LODDebugInfo;
        private readonly ILODSettingsAsset lodSettingsAsset;

        public LODAsset(LODKey lodKey, GameObject root, AssetBundleData assetBundleReference, ILODSettingsAsset lodSettingsAsset)
        {
            LodKey = lodKey;
            Root = root;
            AssetBundleReference = assetBundleReference;
            LoadingFailed = false;
            LODDebugInfo = new LODDebugInfo();
            this.lodSettingsAsset = lodSettingsAsset;

            //This includes list that shouldnt be filled unless we are on debug mode
            if (lodSettingsAsset.IsColorDebuging)
                LODDebugInfo.Update(Root, LodKey.Level, lodSettingsAsset);

            ProfilingCounters.LODAssetAmount.Value++;
        }

        public LODAsset(LODKey lodKey)
        {
            LodKey = lodKey;
            LoadingFailed = true;
            Root = null;
            AssetBundleReference = null;
            lodSettingsAsset = default(LODSettingsAsset);
            LODDebugInfo = default(LODDebugInfo);
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

            if (lodSettingsAsset.IsColorDebuging)
                LODDebugInfo.Update(Root, LodKey.Level, lodSettingsAsset);
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

        public void ToggleDebugColors()
        {
            if (LoadingFailed) return;
            LODDebugInfo.Update(Root, LodKey.Level, lodSettingsAsset);
        }
    }
}
