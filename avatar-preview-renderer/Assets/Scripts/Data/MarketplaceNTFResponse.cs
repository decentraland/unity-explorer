using System;

namespace Data
{
    [Serializable]
    public class MarketplaceNTFResponse
    {
        public Data[] data;

        [Serializable]
        public class Data
        {
            public NFT nft;

            [Serializable]
            public class NFT
            {
                public string urn;
            }
        }
    }
}