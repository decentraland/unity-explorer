using System.Collections.Generic;

namespace ECS.Unity.AssetLoad.Components
{
    /// <summary>
    ///     Tracks which entities are loading assets for this PBAssetLoad component
    ///     Contains the list of asset paths that are being loaded
    /// </summary>
    public struct AssetPreLoadComponent
    {
        public readonly List<string> LoadingAssetPaths;

        public static AssetPreLoadComponent Create() =>
            new (true);

        private AssetPreLoadComponent(bool _)
        {
            LoadingAssetPaths = new List<string>();
        }
    }
}
