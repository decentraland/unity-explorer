using Arch.Core;
using System.Collections.Generic;

namespace ECS.Unity.AssetLoad.Components
{
    /// <summary>
    ///     Tracks which entities are loading assets for this PBAssetLoad component
    ///     Maps asset hash -> loading entity
    /// </summary>
    public struct AssetLoadComponent
    {
        public IReadOnlyList<string> LoadingAssetPaths;
        public Dictionary<string, Entity> LoadingEntities;

        public AssetLoadComponent(IReadOnlyList<string> loadingAssetPaths)
        {
            LoadingAssetPaths = loadingAssetPaths;
            LoadingEntities = new Dictionary<string, Entity>();
        }
    }
}
