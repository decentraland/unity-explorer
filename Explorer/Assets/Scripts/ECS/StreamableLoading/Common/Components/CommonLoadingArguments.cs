using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.WebRequests;
using System.Threading;

namespace ECS.StreamableLoading.Common.Components
{
    public struct CommonLoadingArguments
    {
        public URLAddress URL;
        public URLAddress? CacheableURL;
        public int Attempts;
        public int Timeout;

        /// <summary>
        ///     When the system fails to load from the current source it removes the source from the flags
        /// </summary>
        public AssetSource PermittedSources;
        /// <summary>
        ///     The source the asset is being current loaded from or was loaded from
        /// </summary>
        public AssetSource CurrentSource;

        /// <summary>
        ///     If Custom Sub-directory is not null it should be respected for loading from the Embedded source
        /// </summary>
        public readonly URLSubdirectory CustomEmbeddedSubDirectory;

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public readonly CancellationTokenSource CancellationTokenSource;

        public CommonLoadingArguments(URLAddress url,
            URLSubdirectory customEmbeddedSubDirectory = default,
            int timeout = StreamableLoadingDefaults.TIMEOUT,
            int attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT,
            AssetSource permittedSources = AssetSource.WEB,
            AssetSource currentSource = AssetSource.WEB,
            CancellationTokenSource? cancellationTokenSource = null)
        {
            URL = url;
            CustomEmbeddedSubDirectory = customEmbeddedSubDirectory;
            Timeout = timeout;
            Attempts = attempts;
            PermittedSources = permittedSources;
            CurrentSource = currentSource;
            CacheableURL = null;
            CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        }

        /// <summary>
        ///     Use URLAddress instead of string
        /// </summary>
        public CommonLoadingArguments(string url,
            URLSubdirectory customEmbeddedSubDirectory = default,
            int timeout = StreamableLoadingDefaults.TIMEOUT,
            int attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT,
            AssetSource permittedSources = AssetSource.WEB,
            AssetSource currentSource = AssetSource.WEB,
            CancellationTokenSource cancellationTokenSource = null) :
            this(URLAddress.FromString(url), customEmbeddedSubDirectory, timeout, attempts, permittedSources, currentSource, cancellationTokenSource) { }

        // Always override attempts count for streamable assets as repetitions are handled in LoadSystemBase
        public static implicit operator CommonArguments(in CommonLoadingArguments commonLoadingArguments) =>
            new (commonLoadingArguments.URL, attemptsCount: 1, timeout: commonLoadingArguments.Timeout);

        public string GetCacheableURL()
        {
            //Needed to handle the different versioning and entityIDs of the ABs
            if(CacheableURL.HasValue)
                return CacheableURL.Value.Value;
            return URL;
        }
    }
}
