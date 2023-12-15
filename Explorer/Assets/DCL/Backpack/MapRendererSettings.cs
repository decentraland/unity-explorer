using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Backpack
{
    [Serializable]
    public class BackpackSettings
    {
        [field: SerializeField]
        public AssetReferenceGameObject CategoryIconsMapping { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject RarityBackgroundsMapping { get; private set; }

        [field: SerializeField]
        public AssetReferenceGameObject RarityColorMappings { get; private set; }
    }
}
