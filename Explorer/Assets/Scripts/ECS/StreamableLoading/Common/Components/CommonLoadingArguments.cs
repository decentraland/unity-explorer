using AssetManagement;
using System.Threading;

namespace ECS.StreamableLoading.Common.Components
{
    public struct CommonLoadingArguments
    {
        public string URL;
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

        //public DeferredLoadingState DeferredLoadingState;

        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public readonly CancellationTokenSource CancellationTokenSource;

        public CommonLoadingArguments(string url,
            int timeout = StreamableLoadingDefaults.TIMEOUT,
            int attempts = StreamableLoadingDefaults.ATTEMPTS_COUNT,
            AssetSource permittedSources = AssetSource.WEB,
            AssetSource currentSource = AssetSource.WEB,
            CancellationTokenSource cancellationTokenSource = null)
        {
            URL = url;
            Timeout = timeout;
            Attempts = attempts;
            PermittedSources = permittedSources;
            CurrentSource = currentSource;
            CancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        }
    }
}
