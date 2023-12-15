using UnityEngine;
using Utility;

namespace DCL.Backpack
{
    [CreateAssetMenu(fileName = "NFTColors", menuName = "SO/NFTColors")]
    public class NFTColorsSO : ScriptableObject
    {
        [SerializeField] private SerializableKeyValuePair<string, Color>[] nftColors;
        [SerializeField] private Color defaultColor;

        public Color GetColor(string rarity)
        {
            foreach (var color in nftColors)
            {
                if (color.key == rarity)
                    return color.value;
            }

            return defaultColor;
        }

        public bool DoesRarityExist(string rarity)
        {
            foreach (var color in nftColors)
            {
                if (color.key == rarity)
                    return true;
            }

            return false;
        }
    }
}
