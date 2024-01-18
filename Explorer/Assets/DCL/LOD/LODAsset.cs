using System;
using System.Collections;
using System.Collections.Generic;
using DCL.Profiling;
using ECS.StreamableLoading.AssetBundles;
using UnityEngine;
using Utility;

public struct LODAsset : IDisposable
{
    public string LodKey;
    public GameObject Root;
    public AssetBundleData AssetBundleReference;

    public LODAsset(string lodKey, GameObject root, AssetBundleData assetBundleReference)
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