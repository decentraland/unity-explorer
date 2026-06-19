using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.Utility;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
using SceneRunner.Scene;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleIntention : ILoadingIntention, IEquatable<GetAssetBundleIntention>
    {
        public string? Hash;

        public AssetBundleManifestVersion? AssetBundleManifestVersion;
        public string ParentEntityID;

        /// <summary>
        ///     If the expected object type is null we don't know which asset will be loaded.
        ///     It's valid for dependencies for which we need to load the asset bundle itself only
        /// </summary>
        public readonly Type? ExpectedObjectType;

        /// <summary>
        ///     Left to have a reference of what went wrong in PrepareAssetBundleLoadingParametersSystemBase
        ///     It doesn't participate in the loading process and should not be used for caching or comparison
        /// </summary>
        public readonly string? Name;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        /// <summary>
        ///     Per-file dependency digest from the v49+ scene asset-bundle manifest. Two scenes can request the same
        ///     <see cref="Hash"/> with different dependency closures; this field disambiguates them in the cache.
        ///     Empty for legacy (pre-v49) entries — those keep their historical key.
        /// </summary>
        public string? DepsDigest;

        public bool IsDependency;
        public bool LookForDependencies;

        private GetAssetBundleIntention(Type? expectedObjectType, string? name = null,
            string? hash = null, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default,
            AssetBundleManifestVersion? assetBundleVersion = null,
            string parentEntityID = "",
            bool isDependency = false,
            bool lookForDependencies = false,
            CancellationTokenSource cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;
            ExpectedObjectType = expectedObjectType;

            // Don't resolve URL here

            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, customEmbeddedSubDirectory, permittedSources: permittedSources, cancellationTokenSource: cancellationTokenSource);
            cacheHash = null;
            DepsDigest = null;

            ParentEntityID = parentEntityID;
            AssetBundleManifestVersion = assetBundleVersion;
            IsDependency = isDependency;
            LookForDependencies = lookForDependencies;
        }

        internal GetAssetBundleIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
        }

        public bool Equals(GetAssetBundleIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash)
            && StringComparer.OrdinalIgnoreCase.Equals(DepsDigest ?? string.Empty, other.DepsDigest ?? string.Empty);

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public static GetAssetBundleIntention Create(Type? expectedAssetType, string hash, string name, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (expectedAssetType, hash: hash, name: name, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory);

        public static GetAssetBundleIntention FromHash(string hash, Type? expectedAssetType = null, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default, CancellationTokenSource cancellationTokenSource = null,
            AssetBundleManifestVersion? assetBundleManifestVersion = null, string parentEntityID = "", bool isDependency = false, bool lookForDependencies = false) =>
            new (expectedAssetType, hash: hash, assetBundleVersion: assetBundleManifestVersion, parentEntityID: parentEntityID, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory, isDependency: isDependency, lookForDependencies: lookForDependencies, cancellationTokenSource: cancellationTokenSource);

        public override bool Equals(object obj) =>
            obj is GetAssetBundleIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(Hash ?? string.Empty),
                StringComparer.OrdinalIgnoreCase.GetHashCode(DepsDigest ?? string.Empty));

        public override string ToString() =>
            $"Get Asset Bundle: {Name} ({Hash})";

        public static string BuildInitialSceneStateURL(string initialSceneStateID) =>
            $"staticscene_{initialSceneStateID}{PlatformUtils.GetCurrentPlatform()}";

        public class DiskHashCompute : AbstractDiskHashCompute<GetAssetBundleIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetAssetBundleIntention asset)
            {
                keyPayload.Put(asset.Hash ?? asset.Name!);

                // Only contribute to the disk key when present so legacy 2-part-filename entries keep their existing on-disk file.
                if (!string.IsNullOrEmpty(asset.DepsDigest))
                    keyPayload.Put(asset.DepsDigest);
            }
        }


    }
}
