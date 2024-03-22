using System;

namespace ECS.StreamableLoading.Common.Components
{
    /// <summary>
    ///     The final result of the request
    /// </summary>
    public readonly struct StreamableLoadingResult<T>
    {
        public readonly Exception? Exception;
        public readonly bool Succeeded;
        public readonly T? Asset;

        public StreamableLoadingResult(T asset) : this()
        {
            Asset = asset;
            Succeeded = true;
        }

        public StreamableLoadingResult(Exception exception) : this()
        {
            Exception = exception;
        }

        public bool IsInitialized => Exception != null || Asset != null;
    }
}
