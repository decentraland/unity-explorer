using AssetManagement;
using CommunicationData.URLHelpers;
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

        public string AssetBundleVersion;
        public bool HasParentEntityIDPathInURL;
        public string ParentEntityID;


        /// <summary>
        ///     If the expected object type is null we don't know which asset will be loaded.
        ///     It's valid for dependencies for which we need to load the asset bundle itself only
        /// </summary>
        public readonly Type? ExpectedObjectType;

        /// <summary>
        ///     Left to have a reference of what went wrong in PrepareAssetBundleLoadingParametersSystemBase
        /// </summary>
        public readonly string? Name;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        /// <param name="expectedObjectType"></param>
        /// <param name="name">Name is resolved into Hash before loading by the manifest</param>
        /// <param name="hash">Hash of the asset, if it is provided manifest is not checked</param>
        /// <param name="permittedSources">Sources from which systems will try to load</param>
        /// <param name="assetBundleManifest"></param>
        /// <param name="customEmbeddedSubDirectory"></param>
        /// <param name="cancellationTokenSource"></param>
        /// <summary>
        ///     Used to check if the asset bundle has shader assets in it
        /// </summary>
        public bool LookForShaderAssets;

        public bool SingleAssetBundleHack;
        public bool HasMultipleAssetBundles;

        private GetAssetBundleIntention(Type? expectedObjectType, string? name = null,
            string? hash = null, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default,
            bool lookForShaderAssets = false,
            string assetBundleVersion = "",
            bool hasParentEntityIDPathInURL = false,
            string parentEntityID = "",
            bool singleAssetBundleHack = false,
            bool hasMultipleAssetBundles = false,
            CancellationTokenSource cancellationTokenSource = null)
        {
            Name = name;
            Hash = hash;
            ExpectedObjectType = expectedObjectType;

            // Don't resolve URL here

            CommonArguments = new CommonLoadingArguments(URLAddress.EMPTY, customEmbeddedSubDirectory, permittedSources: permittedSources, cancellationTokenSource: cancellationTokenSource);
            cacheHash = null;
            LookForShaderAssets = lookForShaderAssets;

            AssetBundleVersion = assetBundleVersion;
            HasParentEntityIDPathInURL = hasParentEntityIDPathInURL;
            ParentEntityID = parentEntityID;

            SingleAssetBundleHack = singleAssetBundleHack;
            HasMultipleAssetBundles = hasMultipleAssetBundles;
        }

        internal GetAssetBundleIntention(CommonLoadingArguments commonArguments) : this()
        {
            CommonArguments = commonArguments;
        }

        public bool Equals(GetAssetBundleIntention other) =>
            StringComparer.OrdinalIgnoreCase.Equals(Hash, other.Hash) || Name == other.Name;

        public CommonLoadingArguments CommonArguments { get; set; }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.CancellationTokenSource;

        public static GetAssetBundleIntention Create(Type? expectedAssetType, string hash, string name, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default) =>
            new (expectedAssetType, hash: hash, name: name, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory);

        public static GetAssetBundleIntention FromHash(Type? expectedAssetType, string hash, AssetSource permittedSources = AssetSource.ALL,
            URLSubdirectory customEmbeddedSubDirectory = default, bool lookForShaderAsset = false , CancellationTokenSource cancellationTokenSource = null,
            string assetBundleVersion = "", bool hasParentEntityIDPathInURL = false, string parentEntityID = "") =>
            new (expectedAssetType, hash: hash, assetBundleVersion: assetBundleVersion, hasParentEntityIDPathInURL: hasParentEntityIDPathInURL, parentEntityID: parentEntityID, permittedSources: permittedSources, customEmbeddedSubDirectory: customEmbeddedSubDirectory, lookForShaderAssets: lookForShaderAsset, cancellationTokenSource: cancellationTokenSource);

        public static GetAssetBundleIntention CreateSingleAssetBundleHack(string url) =>
            new (typeof(GameObject), hash: url, singleAssetBundleHack: true, hasMultipleAssetBundles: true, permittedSources: AssetSource.WEB);

        public override bool Equals(object obj) =>
            obj is GetAssetBundleIntention other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(Hash ?? ""), Name);

        public override string ToString() =>
            $"Get Asset Bundle: {Name} ({Hash})";

        public class DiskHashCompute : AbstractDiskHashCompute<GetAssetBundleIntention>
        {
            public static readonly DiskHashCompute INSTANCE = new ();

            private DiskHashCompute() { }

            protected override void FillPayload(IHashKeyPayload keyPayload, in GetAssetBundleIntention asset)
            {
                keyPayload.Put(asset.Hash ?? asset.Name!);
            }
        }


    }
}
