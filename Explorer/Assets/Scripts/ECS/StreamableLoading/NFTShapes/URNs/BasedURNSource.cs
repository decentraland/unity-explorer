using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public class BasedURNSource : IURNSource
    {
        private readonly URLAddress baseUrl;

        public BasedURNSource(DecentralandEnvironment decentralandEnvironment)
            : this(IURNSource.BaseURL(decentralandEnvironment)) { }

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
