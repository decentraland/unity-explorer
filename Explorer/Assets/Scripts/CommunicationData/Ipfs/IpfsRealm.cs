using CommunicationData.URLHelpers;
using System;
using System.Collections.Generic;

namespace Ipfs
{
    public interface IIpfsRealm
    {
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public URLDomain EntitiesActiveEndpoint { get; }
    }

    public class IpfsRealm : IIpfsRealm, IEquatable<IpfsRealm>
    {
        private readonly List<string> sceneUrns;

        public IpfsRealm(URLDomain realmName, IpfsTypes.ServerAbout serverAbout = null)
        {
            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;

            if (serverAbout != null)
            {
                sceneUrns = serverAbout.configurations.scenesUrn;
                ContentBaseUrl = URLDomain.FromString(serverAbout.content.publicUrl);
                EntitiesActiveEndpoint = URLBuilder.Combine(ContentBaseUrl, URLSubdirectory.FromString("entities/active"));
            }
            else
            {
                ContentBaseUrl = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/contents/"));
                EntitiesActiveEndpoint = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/entities/active"));
            }
        }

        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        public IReadOnlyList<string> SceneUrns => sceneUrns;

        public bool Equals(IpfsRealm other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CatalystBaseUrl == other.CatalystBaseUrl;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IpfsRealm)obj);
        }

        public override int GetHashCode() =>
            ContentBaseUrl.GetHashCode();
    }
}
