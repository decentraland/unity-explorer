using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Roads.Settings
{
    [Serializable]
    [CreateAssetMenu(menuName = "Create Road Settings", fileName = "RoadSettings", order = 0)]
    public class RoadSettingsAsset : ScriptableObject, IRoadSettingsAsset
    {
        [field: SerializeField] public List<RoadDescription> RoadDescriptions { get; set; }
        [field: SerializeField] public List<AssetReferenceGameObject> RoadAssetsReference { get; set; }

        IReadOnlyList<RoadDescription> IRoadSettingsAsset.RoadDescriptions => RoadDescriptions;
        IReadOnlyList<AssetReferenceGameObject> IRoadSettingsAsset.RoadAssetsReference => RoadAssetsReference;
    }
}
