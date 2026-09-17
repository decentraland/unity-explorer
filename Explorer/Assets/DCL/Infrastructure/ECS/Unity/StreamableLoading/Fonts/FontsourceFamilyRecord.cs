// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Fonts
{
    // https://api.fontsource.org/v1/fonts/{id}
    [Serializable]
    public class FontsourceFamilyRecord
    {
        public string? npmVersion;

        public List<string>? subsets;

        public Dictionary<string, Dictionary<string, Dictionary<string, Files>>>? variants;

        [Serializable]
        public class Files
        {
            public Urls? url;
        }

        [Serializable]
        public class Urls
        {
            public string? ttf;
        }
    }
}
