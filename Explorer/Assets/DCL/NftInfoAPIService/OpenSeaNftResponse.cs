using System;

namespace DCL.NftInfoAPIService
{
    [Serializable]
    public class OpenSeaNftResponse
    {
        public OpenSeaNftData nft;
    }

    [Serializable]
    public class OpenSeaNftData
    {
        public string identifier;
        public string collection;
        public string image_url;
        public string name;
        public string description;
        public string opensea_url;
        public string contract;
        public NftOwner[] owners;
    }
}
