using System;

namespace DCL.NftInfoAPIService
{
    public struct NftInfo
    {
        public string name;
        public string tokenId;
        public string description;
        public string imageUrl;
        public string assetLink;
        public string marketLink;
        public string marketName;
        public AssetContract assetContract;
        public NftOwner[] owners;

        public bool Equals(string contract, string token) =>
            string.Equals(this.assetContract.address, contract, StringComparison.CurrentCultureIgnoreCase) && this.tokenId == token;
    }

    [Serializable]
    public struct AssetContract
    {
        public string address;
        public string name;
    }

    [Serializable]
    public struct NftOwner
    {
        public string address;
        public int quantity;
    }
}
