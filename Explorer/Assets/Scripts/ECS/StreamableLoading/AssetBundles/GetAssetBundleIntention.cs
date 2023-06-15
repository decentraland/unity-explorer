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
        public readonly bool IsDependency;

        /// <summary>
        ///     Sanitized hash used by Unity's Caching system,
        /// </summary>
        internal Hash128? cacheHash;

        /// <param name="hash">Id of the asset</param>
        /// <param name="isDependency">Indicates that the Bundle will ignore checking the scene manifest</param>
        /// <param name="permittedSources">Sources from which systems will try to load</param>
        public GetAssetBundleIntention(string hash, bool isDependency = false, AssetSource permittedSources = AssetSource.ALL) : this()
        {
            Hash = hash;
            IsDependency = isDependency;
            CommonArguments = new CommonLoadingArguments { PermittedSources = permittedSources };
        }

        public CancellationTokenSource CancellationTokenSource => CommonArguments.cancellationTokenSource;
    }
}
