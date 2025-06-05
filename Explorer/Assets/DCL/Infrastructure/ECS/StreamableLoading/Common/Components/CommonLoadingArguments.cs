using AssetManagement;
using CommunicationData.URLHelpers;
using DCL.WebRequests;
using System;
using System.Threading;

namespace ECS.StreamableLoading.Common.Components
{
    public struct CommonLoadingArguments
    {
        public Uri URL;
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

        public CommonLoadingArguments(Uri url, URLSubdirectory customEmbeddedSubDirectory = default,
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
            CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        }

        // Always override attempts count for streamable assets as repetitions are handled in LoadSystemBase
        public static implicit operator CommonArguments(in CommonLoadingArguments commonLoadingArguments) =>
            new (commonLoadingArguments.URL, attemptsCount: 1, timeout: commonLoadingArguments.Timeout);
    }
}
