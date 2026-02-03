using System.Collections.Generic;

namespace ECS.Unity.AssetLoad.Components
{
    /// <summary>
    ///     Tracks which entities are loading assets for this PBAssetLoad component
    ///     Contains the list of asset paths that are being loaded
    /// </summary>
    public struct AssetPreLoadComponent
    {
        public List<string> LoadingAssetPaths { get; private set; }

        public static AssetPreLoadComponent Empty => new ()
        {
            LoadingAssetPaths = new List<string>()
        };
    }
}
