using UnityEngine;
using Utility;

namespace DCL.MapRenderer.MapLayers.Categories
{
    [CreateAssetMenu(fileName = "MapLayerIconMapping", menuName = "SO/MapLayerIconMapping")]
    public class CategoryIconMappingsSO : ScriptableObject
    {
        [SerializeField] public SerializableKeyValuePair<MapLayer, Sprite>[] nftIcons;
        [SerializeField] public Sprite defaultIcon;

        public Sprite GetCategoryImage(MapLayer nftType)
        {
            foreach (var icon in nftIcons)
                if (icon.key == nftType)
                    return icon.value;

            return defaultIcon;
        }
    }
}
