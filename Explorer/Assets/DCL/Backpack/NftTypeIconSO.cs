using UnityEngine;

namespace DCL.Backpack
{
    [CreateAssetMenu(fileName = "NftTypeIcons", menuName = "SO/NftTypeIcons")]
    public class NftTypeIconSO : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<string, Sprite>[] nftIcons;

        public Sprite GetTypeImage(string nftType)
        {
            foreach (var icon in nftIcons)
            {
                if (icon.key == nftType)
                    return icon.value;
            }

            return null;
        }
    }
}
