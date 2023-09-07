using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Components
{
    public struct GetWearableAssetBundleIntention : ILoadingIntention, IEquatable<GetWearableAssetBundleIntention>
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public string Hash;
        public string BodyShape;
        internal Hash128? cacheHash;

        //TODO: Not completely happy to keep it here
        public readonly SceneAssetBundleManifest WearableAssetBundleManifest;

        private GetWearableAssetBundleIntention(SceneAssetBundleManifest assetBundleManifest, string hash, string bodyShape, AssetSource permittedSources = AssetSource.ALL)
        {
            Hash = hash;
            BodyShape = bodyShape;
            WearableAssetBundleManifest = assetBundleManifest;

            // Don't resolve URL here
            CommonArguments = new CommonLoadingArguments(string.Empty, permittedSources: permittedSources);
            cacheHash = null;
        }

        public bool Equals(GetWearableAssetBundleIntention other) =>
            Equals(CancellationTokenSource, other.CancellationTokenSource);

        public override bool Equals(object obj) =>
            obj is GetWearableAssetBundleIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(WearableAssetBundleManifest, Hash, CancellationTokenSource, CommonArguments);

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public static GetWearableAssetBundleIntention FromHash(SceneAssetBundleManifest sceneAssetBundleManifest, string hash, string bodyShape, AssetSource permittedSources = AssetSource.ALL) =>
            new (sceneAssetBundleManifest, hash, bodyShape, permittedSources: permittedSources);
    }
}
