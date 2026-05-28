using AssetManagement;
using DCL.Diagnostics;
using System;
using UnityEngine;
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

            /// <summary>
            ///     True when the request reached a terminal successful (or fallback-acceptable) state.
            ///     False when the request failed or was cancelled; check <see cref="Cancelled"/>
            ///     to distinguish a transient cancellation from a sticky failure.
            /// </summary>
            public readonly bool Succeeded;

            /// <summary>
            ///     True when the request was cancelled mid-flight (transient state — consumers can
            ///     clear the slot and retry). False for both genuine successes and sticky failures.
            /// </summary>
            public readonly bool Cancelled;

            public WithFallback(T asset)
            {
                Asset = asset;
                initialized = true;
                Succeeded = true;
                Cancelled = false;
            }

            private WithFallback(T asset, bool initialized, bool succeeded, bool cancelled)
            {
                Asset = asset;
                this.initialized = initialized;
                Succeeded = succeeded;
                Cancelled = cancelled;
            }

            public static WithFallback Failed() =>
                new (default!, initialized: true, succeeded: false, cancelled: false);

            public static WithFallback CancelledResult() =>
                new (default!, initialized: true, succeeded: false, cancelled: true);

            /// <summary>
            ///     Can be uninitialized if structure was created with default constructor
            /// </summary>
            public bool IsInitialized => initialized;

            public static implicit operator StreamableLoadingResult<T>(WithFallback withFallback) =>
                withFallback.IsInitialized && withFallback.Succeeded ? new StreamableLoadingResult<T>(withFallback.Asset) : new StreamableLoadingResult<T>();
        }

        private readonly (ReportData reportData, Exception exception)? exceptionData;

        public readonly bool Succeeded;
        public readonly T? Asset;
        public Exception? Exception => exceptionData?.exception;
        public ReportData ReportData => exceptionData?.reportData ?? ReportData.UNSPECIFIED;

        public StreamableLoadingResult(T? asset) : this()
        {
            Asset = asset;
            Succeeded = true;
        }

        public StreamableLoadingResult(ReportData reportData, Exception exception) : this()
        {
            if (exception is not OperationCanceledException)
            {
                if (exception is StreamableLoadingException streamableLoadingException)
                {
                    switch (streamableLoadingException.Severity)
                    {
                        case LogType.Exception:
                            ReportHub.LogException(exception, reportData);
                            break;
                        default:
                            ReportHub.Log(streamableLoadingException.Severity, reportData, exception.ToString());
                            break;
                    }
                }
            }

            exceptionData = (reportData, exception);
        }

        /// <summary>
        ///     Logs exception if unsuccessful and it wasn't already logged
        /// </summary>
        public void TryLogException(ReportData? overrideReportData = null)
        {
            if (!Succeeded && exceptionData is { exception: not StreamableLoadingException or OperationCanceledException })
                ReportHub.LogException(exceptionData.Value.exception, overrideReportData ?? exceptionData.Value.reportData);
        }

        public bool IsInitialized => Exception != null || Asset != null || Succeeded;

        public override string ToString() =>
            IsInitialized ? Succeeded ? Asset!.ToString() : Exception!.ToString() : "Not Initialized";

    }
}
