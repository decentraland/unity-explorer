using CommunicationData.URLHelpers;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public interface IURNSource
    {
        const string BASE_URL = "https://opensea.decentraland.org/api/v2/chain/{chain}/contract/{address}/nfts/{id}";

        URLAddress UrlOrEmpty(URN urn);

        class Fake : IURNSource
        {
            private readonly URLAddress url;

            public Fake(string url) : this(URLAddress.FromString(url)) { }

            public Fake(URLAddress url)
            {
                this.url = url;
            }

            public URLAddress UrlOrEmpty(URN urn) =>
                url;
        }
    }
}
