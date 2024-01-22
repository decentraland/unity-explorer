using CommunicationData.URLHelpers;
using DCL.Diagnostics;
using System;

namespace ECS.StreamableLoading.NftShapes.Urns
{
    public class BasedUrnSource : IUrnSource
    {
        private readonly URLAddress baseUrl;

        public BasedUrnSource(string baseUrl = "https://opensea.decentraland.org/api/v2/chain/ethereum/contract/{address}/nfts/{id}")
            : this(URLAddress.FromString(baseUrl)) { }

        public BasedUrnSource(URLAddress baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public URLAddress UrlOrEmpty(URN urn) =>
            urn.ToUrlOrEmpty(baseUrl);
    }
}
