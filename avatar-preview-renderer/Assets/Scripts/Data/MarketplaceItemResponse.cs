using System;

namespace Data
{
    [Serializable]
    public class MarketplaceItemResponse
    {
        public Data[] data;

        [Serializable]
        public class Data
        {
            public string urn;
        }
    }
}