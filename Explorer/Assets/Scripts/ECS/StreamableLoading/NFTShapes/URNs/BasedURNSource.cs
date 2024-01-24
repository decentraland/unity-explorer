using CommunicationData.URLHelpers;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public class BasedURNSource : IURNSource
    {
        private readonly URLAddress baseUrl;

        public BasedURNSource(string baseUrl = "https://opensea.decentraland.org/api/v2/chain/ethereum/contract/{address}/nfts/{id}")
            : this(URLAddress.FromString(baseUrl)) { }

        public BasedURNSource(URLAddress baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public URLAddress UrlOrEmpty(URN urn) =>
            urn.ToUrlOrEmpty(baseUrl);
    }
}
