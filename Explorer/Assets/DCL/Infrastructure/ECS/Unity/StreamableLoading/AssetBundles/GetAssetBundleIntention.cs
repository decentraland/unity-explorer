using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.Ipfs;
using ECS.StreamableLoading.Cache.Disk.Cacheables;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleIntention : ILoadingIntention, IEquatable<GetAssetBundleIntention>
    {
        public string? Hash;

        public string ParentEntityID;

        //Backing for AssetBundleManifest — nullable because default-initialized structs zero reference fields; set by every factory.
        private AssetBundleManifestVersion? assetBundleManifest;

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

        public bool IsDependency;
        public bool LookForDependencies;

        private GetAssetBundleIntention(Type? expectedObjectType, AssetBundleManifestVersion assetBundle, string? name = null,
            string? hash = null, AssetSource permittedSources = AssetSource.All,
            URLSubdirectory customEmbeddedSubDirectory = default,
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

            ParentEntityID = parentEntityID;
            assetBundleManifest = assetBundle;
            IsDependency = isDependency;
            LookForDependencies = lookForDependencies;
        }

        internal GetAssetBundleIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
        }

        /// <summary>An AB can never be requested without a manifest: every factory sets one, and default-initialized structs (which never flow into loading) observe the failed sentinel.</summary>
        public readonly AssetBundleManifestVersion AssetBundleManifest => assetBundleManifest ?? AssetBundleManifestVersion.FAILED;

        // Hash alone identifies the bundle: v49+ hashes carry the deps digest inside the file name, so two dependency closures never share a Hash.
        public bool Equals(GetAssetBundleIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash);

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public static GetAssetBundleIntention Create(Type? expectedAssetType, string hash, string name, AssetBundleManifestVersion assetBundleManifestVersion, string parentEntityID,
            AssetSource permittedSources = AssetSource.All,
            URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (expectedAssetType, assetBundleManifestVersion, hash: hash, name: name, parentEntityID: parentEntityID, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory);

        public static GetAssetBundleIntention FromHash(string hash, AssetBundleManifestVersion assetBundleManifestVersion, Type? expectedAssetType = null, AssetSource permittedSources = AssetSource.All,
            URLSubdirectory customEmbeddedSubDirectory = default, CancellationTokenSource cancellationTokenSource = null,
            string parentEntityID = "", bool isDependency = false, bool lookForDependencies = false) =>
            new (expectedAssetType, assetBundleManifestVersion, hash: hash, parentEntityID: parentEntityID, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory, isDependency: isDependency, lookForDependencies: lookForDependencies, cancellationTokenSource: cancellationTokenSource);

        public override bool Equals(object obj) =>
            obj is GetAssetBundleIntention other && Equals(other);

        public override int GetHashCode() =>
            StringComparer.OrdinalIgnoreCase.GetHashCode(Hash ?? string.Empty);

        public override string ToString() =>
            $"Get Asset Bundle: {Name} ({Hash})";

        public class DiskHashCompute : AbstractDiskHashCompute<GetAssetBundleIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetAssetBundleIntention asset)
            {
                // The hash alone keys the on-disk file (v49+ hashes embed the digest); digest-less hashes keep their legacy key so existing entries keep hitting.
                keyPayload.Put(asset.Hash ?? asset.Name!);
            }
        }


    }
}
