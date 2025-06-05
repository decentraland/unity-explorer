using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace ECS.StreamableLoading.NFTShapes.URNs
{
    public class BasedURNSource : IURNSource
    {
        private readonly Uri baseUrl;

        public BasedURNSource(IDecentralandUrlsSource decentralandUrlsSource)
            : this(IURNSource.BaseURL(decentralandUrlsSource)) { }

        public BasedURNSource(Uri baseUrl)
        {
            this.baseUrl = baseUrl;
        }

        public Uri? UrlOrEmpty(URN urn) =>
            urn.ToUrlOrEmpty(baseUrl);
    }
}
