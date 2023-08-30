using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

public struct GetWearableByPointersIntention : ILoadingIntention, IEquatable<GetWearableByPointersIntention>
{
    public CancellationTokenSource CancellationTokenSource { get; }
    public CommonLoadingArguments CommonArguments { get; set; }
    public string[] Pointers;

    public bool Equals(GetWearableByPointersIntention other) =>
        Equals(Pointers, other.Pointers) && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableByPointersIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Pointers, CancellationTokenSource, CommonArguments);
}

public struct GetWearableByParamIntention : ILoadingIntention, IEquatable<GetWearableByParamIntention>
{
    public CancellationTokenSource CancellationTokenSource { get; }
    public CommonLoadingArguments CommonArguments { get; set; }

    //ValidParams: pageNum, pageSize, includeEntities (bool), rarity, categofy, name, orderBy, direction,
    //collectionType (base-wearable, on-chain, third-party), thirdPartyCollectionId
    public (string, string)[] Params;
    public string UserID;

    public bool Equals(GetWearableByParamIntention other) =>
        Equals(Params, other.Params) && UserID == other.UserID && Equals(CancellationTokenSource, other.CancellationTokenSource) && CommonArguments.Equals(other.CommonArguments);

    public override bool Equals(object obj) =>
        obj is GetWearableByParamIntention other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Params, UserID, CancellationTokenSource, CommonArguments);
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
