using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public string Hash;

        /// <summary>
        ///     Name not resolved into <see cref="Hash" />
        /// </summary>
        public readonly string Name;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        /// <param name="hash">Hash of the asset, if it is provided manifest is not checked</param>
        /// <param name="name">Name is resolved into Hash before loading by the manifest</param>
        /// <param name="permittedSources">Sources from which systems will try to load</param>
        private GetAssetBundleIntention(string name = null, string hash = null, AssetSource permittedSources = AssetSource.ALL)
        {
            Name = name;
            Hash = hash;

            // Don't resolve URL here

            CommonArguments = new CommonLoadingArguments(string.Empty, permittedSources: permittedSources);

            cacheHash = null;
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.cancellationTokenSource;

        public static GetAssetBundleIntention FromName(string name, AssetSource permittedSources = AssetSource.ALL) =>
            new (name: name, permittedSources: permittedSources);

        public static GetAssetBundleIntention FromHash(string hash, AssetSource permittedSources = AssetSource.ALL) =>
            new (hash: hash, permittedSources: permittedSources);
    }
}
