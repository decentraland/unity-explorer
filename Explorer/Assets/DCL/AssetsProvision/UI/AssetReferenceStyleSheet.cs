using System;
using UnityEngine.AddressableAssets;
using UnityEngine.UIElements;

namespace DCL.AssetsProvision
{
    [Serializable]
    public class AssetReferenceStyleSheet : AssetReferenceT<StyleSheet>
    {
        public AssetReferenceStyleSheet(string guid) : base(guid) { }
    }
}
