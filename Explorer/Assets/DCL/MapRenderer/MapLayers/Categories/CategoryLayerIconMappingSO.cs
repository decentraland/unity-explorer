using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Categories
{
    [CreateAssetMenu(fileName = "MapLayerCategoryIconMapping", menuName = "SO/MapLayerCategoryIconMapping")]
    public class CategoryLayerIconMappingsSO : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<CategoriesEnum, Sprite>[] nftIcons;
        [SerializeField] public Sprite defaultIcon;

        public Sprite GetCategoryImage(CategoriesEnum nftType)
        {
            foreach (var icon in nftIcons)
                if (icon.key == nftType)
                    return icon.value;

            return defaultIcon;
        }
    }
}
