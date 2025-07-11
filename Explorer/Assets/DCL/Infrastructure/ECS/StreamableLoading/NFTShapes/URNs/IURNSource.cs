using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public interface IURNSource
    {
        Uri? UrlOrEmpty(URN urn);

        public static Uri BaseURL(IDecentralandUrlsSource decentralandUrlsSource) =>
            decentralandUrlsSource.Url(DecentralandUrl.OpenSea).Append("/api/v2/chain/{chain}/contract/{address}/nfts/{id}");
    }
}
