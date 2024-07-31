using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public interface IURNSource
    {
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

        public static string BaseURL(DecentralandEnvironment environment) =>
            $"https://opensea.decentraland.{environment.ToString()!.ToLower()}/api/v2/chain/{{chain}}/contract/{{address}}/nfts/{{id}}";
    }
}
