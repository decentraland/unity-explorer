using System.Collections.Generic;

namespace ECS.Unity.AssetLoad.Components
{
    /// <summary>
    ///     Tracks which entities are loading assets for this PBAssetLoad component
    ///     Maps asset hash -> loading entity
    /// </summary>
    public struct AssetLoadComponent
    {
        public readonly List<string> LoadingAssetPaths;

        public static AssetLoadComponent Create() =>
            new (true);

        private AssetLoadComponent(bool _)
        {
            LoadingAssetPaths = new List<string>();
        }
    }
}
