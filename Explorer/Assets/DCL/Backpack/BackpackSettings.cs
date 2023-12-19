using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Backpack
{
    [Serializable]
    public class BackpackSettings
    {
        [field: SerializeField]
        public NftTypeIconSO CategoryIconsMapping { get; private set; }

        [field: SerializeField]
        public NftTypeIconSO RarityBackgroundsMapping { get; private set; }

        [field: SerializeField]
        public NFTColorsSO RarityColorMappings { get; private set; }
    }
}
