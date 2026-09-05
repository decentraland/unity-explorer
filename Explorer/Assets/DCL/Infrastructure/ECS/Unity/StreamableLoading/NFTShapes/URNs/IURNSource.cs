using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public interface IURNSource
    {
        URLAddress UrlOrEmpty(URN urn);

        public static string BaseURL(IDecentralandUrlsSource decentralandUrlsSource) =>
            $"{decentralandUrlsSource.Url(DecentralandUrl.OpenSea)}/api/v2/chain/{{chain}}/contract/{{address}}/nfts/{{id}}";
    }
}
