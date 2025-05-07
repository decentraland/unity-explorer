namespace ECS.StreamableLoading.Common.Components
{
    public readonly struct OngoingRequestResult<T>
    {
        public readonly PartialLoadingState? PartialDownloadingData;
        public readonly StreamableLoadingResult<T>? Result;

        public OngoingRequestResult(PartialLoadingState? partialDownloadingState, StreamableLoadingResult<T>? result)
        {
            PartialDownloadingData = partialDownloadingState;
            Result = result;
        }
    }
}
