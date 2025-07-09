using DCL.WebRequests;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public readonly PartialDownloadStream PartialDownloadStream;

        private bool ownershipTransferred;

        public PartialLoadingState(PartialDownloadStream partialDownloadStream)
        {
            PartialDownloadStream = partialDownloadStream;
            ownershipTransferred = false;
        }

        /// <summary>
        ///     When the memory ownership is transferred, the responsibility to dispose of the memory will be on the external caller
        /// </summary>
        internal PartialDownloadStream TransferMemoryOwnership()
        {
            ownershipTransferred = true;
            return PartialDownloadStream;
        }

        public void Dispose()
        {
            if (ownershipTransferred) return;
            PartialDownloadStream.DisposeAsync();
        }
    }
}
