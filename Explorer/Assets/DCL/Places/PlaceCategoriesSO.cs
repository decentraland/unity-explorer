using System;
using UnityEngine;

namespace DCL.Places
{
    [CreateAssetMenu(fileName = "PlaceCategories", menuName = "DCL/Places/Place Categories")]
    public class PlaceCategoriesSO : ScriptableObject
    {
        [SerializeField] public PlaceCategoryData[] categories;

        public string GetCategoryName(string categoryId)
        {
            foreach (var category in categories)
                if (category.id == categoryId)
                    return category.name;

            return string.Empty;
        }

        [Serializable]
        public class PlaceCategoryData
        {
            public string id;
            public string name;
            public Sprite icon;
        }
    }
}
