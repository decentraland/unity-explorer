using AssetManagement;
using DCL.Diagnostics;
using System;
using System.Runtime.CompilerServices;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     The final result of the request
    /// </summary>
    public readonly struct StreamableLoadingResult<T>
    {
        /// <summary>
        ///     Always contains result even if the request has failed
        /// </summary>
        public readonly struct WithFallback
        {
            public readonly T Asset;

            /// <summary>
            ///     Initialized won't be set if default constructor was called
            /// </summary>
            private readonly bool initialized;

            public WithFallback(T asset)
            {
                Asset = asset;
                initialized = true;
            }

            /// <summary>
            ///     Can be uninitialized if structure was created with default constructor
            /// </summary>
            public bool IsInitialized => initialized;

            public static implicit operator StreamableLoadingResult<T>(WithFallback withFallback) =>
                withFallback.IsInitialized ? new StreamableLoadingResult<T>(withFallback.Asset) : new StreamableLoadingResult<T>();
        }

        private readonly (ReportData reportData, Exception exception)? exceptionData;

        public readonly bool Succeeded;
        public readonly T? Asset;
        public Exception? Exception => exceptionData?.exception;
        public ReportData ReportData => exceptionData?.reportData ?? ReportData.UNSPECIFIED;

#if STREAMABLE_LOADING_SOURCE_DEBUG
        public readonly AssetSource Source;
#endif

        public StreamableLoadingResult(T? asset, AssetSource source = AssetSource.NONE) : this()
        {
            Asset = asset;
            Succeeded = true;

#if STREAMABLE_LOADING_SOURCE_DEBUG
            this.Source = source;

            if (source is not AssetSource.NONE)
                ReportHub.Log(
                    ReportData.UNSPECIFIED,
                    $"StreamableLoadingResult source: {source} for asset: {asset?.GetType().Name} {asset?.ToString() ?? "null"}"
                );
#endif
        }

        public StreamableLoadingResult(ReportData reportData, Exception exception) : this()
        {
            if (exception is not OperationCanceledException)
                ReportHub.LogException(exception, reportData);

            exceptionData = (reportData, exception);
        }

        public bool IsInitialized => Exception != null || Asset != null || Succeeded;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StreamableLoadingResult<T> WithSource(AssetSource source) =>
            this.Succeeded ? new StreamableLoadingResult<T>(Asset, source) : this;

        public override string ToString() =>
            IsInitialized ? Succeeded ? Asset!.ToString() : Exception!.ToString() : "Not Initialized";
    }
}
