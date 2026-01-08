using System;

namespace DCL.PlacesAPIService
{
    [Serializable]
    public class PlacesCategoriesAPIResponse
    {
        public bool ok;
        public PlaceCategoryData[] data;
    }

    [Serializable]
    public class PlaceCategoryData
    {
        public string name;
        public int count;
        public PlaceCategoryLocalizationData i18n;
    }

    [Serializable]
    public class PlaceCategoryLocalizationData
    {
        public string en;
    }
}
