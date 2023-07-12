using System;
using System.Collections.Generic;

namespace Ipfs
{
    public interface IIpfsRealm
    {
        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }
        public IReadOnlyList<string> SceneUrns { get; }
        public string EntitiesActiveEndpoint { get; }
    }

    public class IpfsRealm : IIpfsRealm, IEquatable<IpfsRealm>
    {
        private readonly List<string> sceneUrns;

        public IpfsRealm(string realmName, IpfsTypes.ServerAbout serverAbout = null)
        {
            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;

            if (serverAbout != null)
            {
                sceneUrns = serverAbout.configurations.scenesUrn;
                ContentBaseUrl = serverAbout.content.publicUrl;

                if (!ContentBaseUrl.EndsWith("/"))
                    ContentBaseUrl += "/";

                EntitiesActiveEndpoint = ContentBaseUrl + "entities/active";
            }
            else
            {
                ContentBaseUrl = CatalystBaseUrl + "content/contents/";
                EntitiesActiveEndpoint = CatalystBaseUrl + "content/entities/active";
            }
        }

        public string CatalystBaseUrl { get; }
        public string ContentBaseUrl { get; }

        public string EntitiesActiveEndpoint { get; }

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
            CatalystBaseUrl != null ? CatalystBaseUrl.GetHashCode() : 0;
    }
}
