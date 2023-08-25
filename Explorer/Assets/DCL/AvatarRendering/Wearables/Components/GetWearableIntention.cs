using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

public struct GetWearableIntention : ILoadingIntention, IEquatable<GetWearableIntention>
{
    public CancellationTokenSource CancellationTokenSource { get; }
    public CommonLoadingArguments CommonArguments { get; set; }
    public string Pointer;

    public bool Equals(GetWearableIntention other) =>
        Equals(Pointer, other.Pointer) && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Pointer, CancellationTokenSource, CommonArguments);
}

public struct GetWearableAssetBundleIntention : ILoadingIntention, IEquatable<GetWearableAssetBundleIntention>
{
    public CommonLoadingArguments CommonArguments { get; set; }

    public string Hash;
    public SceneAssetBundleManifest AssetBundleManifest;
    internal Hash128? cacheHash;

    private GetWearableAssetBundleIntention(SceneAssetBundleManifest assetBundleManifest, string hash = null, AssetSource permittedSources = AssetSource.ALL)
    {
        Hash = hash;

        AssetBundleManifest = assetBundleManifest;

        // Don't resolve URL here
        CommonArguments = new CommonLoadingArguments(string.Empty, permittedSources: permittedSources);
        cacheHash = null;
    }

    public bool Equals(GetWearableAssetBundleIntention other) =>
        Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableAssetBundleIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(CancellationTokenSource, CommonArguments);

    public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

    public static GetWearableAssetBundleIntention FromHash(SceneAssetBundleManifest assetBundleManifest, string hash, AssetSource permittedSources = AssetSource.ALL) =>
        new (assetBundleManifest, hash, permittedSources: permittedSources);
}
