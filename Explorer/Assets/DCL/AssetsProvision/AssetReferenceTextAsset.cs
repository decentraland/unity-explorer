using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.AssetsProvision
{
    [Serializable]
    public class AssetReferenceTextAsset : AssetReferenceT<TextAsset>
    {
        public AssetReferenceTextAsset(string guid) : base(guid) { }
    }
}
