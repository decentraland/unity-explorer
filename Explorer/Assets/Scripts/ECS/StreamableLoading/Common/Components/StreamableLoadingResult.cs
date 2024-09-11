using DCL.Diagnostics;
using System;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     The final result of the request
    /// </summary>
    public readonly struct StreamableLoadingResult<T>
    {
        private readonly (ReportData reportData, Exception exception)? exceptionData;

        public readonly bool Succeeded;
        public readonly T? Asset;

        public Exception? Exception => exceptionData?.exception;

        public ReportData ReportData => exceptionData?.reportData ?? ReportData.UNSPECIFIED;

        public StreamableLoadingResult(T asset) : this()
        {
            Asset = asset;
            Succeeded = true;
        }

        public StreamableLoadingResult(ReportData reportData, Exception exception) : this()
        {
            if (exception is not OperationCanceledException)
                ReportHub.LogException(exception, reportData);

            exceptionData = (reportData, exception);
        }

        public bool IsInitialized => Exception != null || Asset != null || Succeeded;

        public override string ToString() =>
            IsInitialized ? Succeeded ? Asset!.ToString() : Exception!.ToString() : "Not Initialized";
    }
}
