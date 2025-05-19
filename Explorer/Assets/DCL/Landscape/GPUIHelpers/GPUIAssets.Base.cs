using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "GPUIAsset", menuName = "DCL/Landscape/GPUI Asset")]
public partial class GPUIAssets : ScriptableObject
{
    [Serializable]
    public class GPUIAssetsRef : AssetReferenceT<GPUIAssets>
    {
        public GPUIAssetsRef(string guid) : base(guid) { }
    }
}