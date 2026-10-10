using System;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace DCL.AssetsProvision
{
    [Serializable]
    public class AssetReferenceVisualTreeAsset : AssetReferenceT<VisualTreeAsset>
    {
        public AssetReferenceVisualTreeAsset(string guid) : base(guid) { }
    }
}
