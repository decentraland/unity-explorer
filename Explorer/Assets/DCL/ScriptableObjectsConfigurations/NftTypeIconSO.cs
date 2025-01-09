using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    [CreateAssetMenu(fileName = "NftTypeIcons", menuName = "DCL/Backpack/NFT Type Icons")]
    public class NftTypeIconSO : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<string, Sprite>[] nftIcons;
        [SerializeField] public Sprite defaultIcon;

        public Sprite GetTypeImage(string? nftType)
        {
            if (string.IsNullOrEmpty(nftType)) return defaultIcon;

            foreach (SerializableKeyValuePair<string, Sprite> icon in nftIcons)
                if (icon.key == nftType)
                    return icon.value;

            return defaultIcon;
        }
    }
}
