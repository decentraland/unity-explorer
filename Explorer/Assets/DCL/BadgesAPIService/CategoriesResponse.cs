using System;
using System.Collections.Generic;

namespace DCL.BadgesAPIService
{
    [Serializable]
    public class CategoriesResponse
    {
        public CategoriesData data;
    }

    [Serializable]
    public class CategoriesData
    {
        public List<string> categories;
    }
}
