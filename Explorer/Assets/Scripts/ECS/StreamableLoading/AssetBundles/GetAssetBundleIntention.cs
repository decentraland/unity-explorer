using AssetManagement;
using ECS.StreamableLoading.Common.Components;
using System.Threading;
using UnityEngine;

namespace ECS.StreamableLoading.AssetBundles
{
    public struct GetAssetBundleIntention : ILoadingIntention
    {
        public CommonLoadingArguments CommonArguments { get; set; }

        public readonly string Hash;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        internal readonly CancellationTokenSource cancellationTokenSource;

        /// <param name="hash">Id of the asset</param>
        /// <param name="permittedSources">Sources from which systems will try to load</param>
        public GetAssetBundleIntention(string hash, AssetSource permittedSources = AssetSource.ALL) : this()
        {
            Hash = hash;
            CommonArguments = new CommonLoadingArguments { PermittedSources = permittedSources };
        }
    }
}
