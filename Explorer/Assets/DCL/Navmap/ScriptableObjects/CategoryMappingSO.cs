using DCL.MapRenderer.MapLayers.Categories;
using UnityEngine;
using Utility;

namespace DCL.Navmap.ScriptableObjects
{
    [CreateAssetMenu(fileName = "MapCategoryIconMapping", menuName = "SO/MapCategoryIconMapping")]
    public class CategoryMappingSO : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<CategoriesEnum, Sprite>[] categoryIcons;
        [SerializeField] public Sprite defaultIcon;

        public Sprite GetCategoryImage(CategoriesEnum category)
        {
            foreach (var icon in categoryIcons)
                if (icon.key == category)
                    return icon.value;

            return defaultIcon;
        }
    }
}
