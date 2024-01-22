using CommunicationData.URLHelpers;

namespace ECS.StreamableLoading.NftShapes.Urns
{
    public interface IUrnSource
    {
        URLAddress UrlOrEmpty(URN urn);

        class Fake : IUrnSource
        {
            private readonly URLAddress url;

            public Fake(string url) : this(URLAddress.FromString(url))
            {
            }

            public Fake(URLAddress url)
            {
                this.url = url;
            }

            public URLAddress UrlOrEmpty(URN urn) =>
                url;
        }
    }
}
