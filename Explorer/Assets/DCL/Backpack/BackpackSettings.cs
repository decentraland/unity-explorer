using DCL.AssetsProvision;
using DCL.UI;
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

        [field: SerializeField]
        public AssetReferenceT<ColorPresetsSO> HairColors { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<ColorPresetsSO> EyesColors { get; private set; }

        [field: SerializeField]
        public AssetReferenceT<ColorPresetsSO> BodyshapeColors { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject PageButtonView { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject ColorToggle { get; private set; }
    }
}
