using UnityEngine;
using Utility;

namespace DCL.Places
{
    [CreateAssetMenu(fileName = "PlaceCategoryIconsMapping", menuName = "DCL/Places/Place Category Icons Mapping")]
    public class PlaceCategoryIconsMapping : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<string, Sprite>[] categoryIcons;
        [SerializeField] public Sprite defaultIcon;

        public Sprite? GetCategoryImage(string categoryId)
        {
            foreach (var icon in categoryIcons)
                if (icon.key == categoryId)
                    return icon.value;

            return null;
        }
    }
}
