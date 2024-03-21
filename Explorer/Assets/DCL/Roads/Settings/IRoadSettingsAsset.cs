using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Roads.Settings
{
    public interface IRoadSettingsAsset
    {
        public List<RoadDescription> RoadDescriptions { get; set; }
        public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }
    }
}