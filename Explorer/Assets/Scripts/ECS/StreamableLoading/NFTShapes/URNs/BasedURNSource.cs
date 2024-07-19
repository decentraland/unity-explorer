using CommunicationData.URLHelpers;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public class BasedURNSource : IURNSource
    {
        private readonly URLAddress baseUrl;

        public BasedURNSource() : this(IURNSource.BASE_URL) { }

        public BasedURNSource(string baseUrl)
            : this(URLAddress.FromString(baseUrl)) { }

        public BasedURNSource(URLAddress baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public URLAddress UrlOrEmpty(URN urn) =>
            urn.ToUrlOrEmpty(baseUrl);
    }
}
