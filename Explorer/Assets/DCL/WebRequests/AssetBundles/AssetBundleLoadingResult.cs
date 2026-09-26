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

        public AssetBundleLoadingResult(AssetBundle? assetBundle, string? dataProcessingError)
        {
            AssetBundle = assetBundle;
            DataProcessingError = dataProcessingError;
        }
    }
}
