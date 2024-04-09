using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Roads.Settings
{
    public interface IRoadSettingsAsset
    {
        public IReadOnlyList<RoadDescription> RoadDescriptions { get; }
        public IReadOnlyList<AssetReferenceGameObject> RoadAssetsReference { get; }
    }
}
