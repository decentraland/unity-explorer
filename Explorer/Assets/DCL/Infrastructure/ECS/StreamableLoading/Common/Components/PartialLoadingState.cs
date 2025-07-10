using DCL.WebRequests;

namespace ECS.StreamableLoading.Common.Components
{
    public struct PartialLoadingState
    {
        public readonly PartialDownloadStream PartialDownloadStream;

        public PartialLoadingState(PartialDownloadStream partialDownloadStream)
        {
            PartialDownloadStream = partialDownloadStream;
            ownershipTransferred = false;
        }

        /// <summary>
        ///     <list type="bullet">
        ///         <item>Ownership can be transferred only when the stream is fully ready</item>
        ///         <item>If ownership is transferred the lifecycle of the stream is controlled by the specific loading system</item>
        ///         <item>Otherwise the stream will be disposed when the related entity is destroyed</item>
        ///     </list>
        /// </summary>
        internal bool ownershipTransferred { get; private set; }

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
