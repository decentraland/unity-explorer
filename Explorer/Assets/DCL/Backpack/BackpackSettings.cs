using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Backpack
{
    [Serializable]
    public class BackpackSettings
    {
        [field: SerializeField]
        public AssetReferenceT<NftTypeIconSO> CategoryIconsMapping { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<NftTypeIconSO> RarityBackgroundsMapping { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<NftTypeIconSO> RarityInfoPanelBackgroundsMapping { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<NFTColorsSO> RarityColorMappings { get; private set; }
    }
}
