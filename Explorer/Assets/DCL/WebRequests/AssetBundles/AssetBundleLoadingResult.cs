using UnityEngine;

namespace DCL.WebRequests
{
    public readonly struct AssetBundleLoadingResult
    {
        public readonly AssetBundle? AssetBundle;

        /// <summary>
        ///     Data processing error is set if AssetBundle is null
        /// </summary>
        public readonly string? DataProcessingError;

        /// <summary>
        ///     Bytes downloaded for this asset bundle request.
        /// </summary>
        public readonly ulong DownloadedBytes;

        /// <summary>
        ///     Content-Length from the response header. -1 if unavailable.
        /// </summary>
        public readonly long ContentLength;

        public AssetBundleLoadingResult(AssetBundle? assetBundle, string? dataProcessingError, ulong downloadedBytes = 0, long contentLength = -1)
        {
            AssetBundle = assetBundle;
            DataProcessingError = dataProcessingError;
            DownloadedBytes = downloadedBytes;
            ContentLength = contentLength;
        }
    }
}
