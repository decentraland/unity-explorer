// ReSharper disable InconsistentNaming

using System.Collections.Generic;

namespace ECS.StreamableLoading.Fonts
{
    // https://api.fontsource.org/v1/fonts/{id}
    public class FontsourceFamilyRecord
    {
        public string? npmVersion;

        public List<string>? subsets;

        public Dictionary<string, Dictionary<string, Dictionary<string, Files>>>? variants;

        public class Files
        {
            public Urls? url;
        }

        public class Urls
        {
            public string? ttf;
        }
    }
}
