using CommunicationData.URLHelpers;
using System;

namespace DCL.Ipfs
{
    public readonly struct IpfsPath
    {
        public readonly string EntityId;
        public readonly URLDomain BaseUrl;

        public IpfsPath(string entityId, URLDomain baseUrl)
        {
            EntityId = entityId;
            BaseUrl = baseUrl;
        }

        public Uri GetUrl(URLDomain defaultContentUrl)
        {
            var entityAsPath = URLPath.FromString(EntityId);
            return URLBuilder.Combine(!BaseUrl.IsEmpty ? BaseUrl : defaultContentUrl, entityAsPath);
        }

        public override string ToString() =>
            $"IpfsPath (EntityId: {EntityId}, BaseUrl: {BaseUrl.Value})";
    }
}
