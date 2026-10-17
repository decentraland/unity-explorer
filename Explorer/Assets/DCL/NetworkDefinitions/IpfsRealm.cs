using CommunicationData.URLHelpers;
using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Collections.Generic;

namespace DCL.Ipfs
{
    /// <summary>
    ///     Contains Raw urls received from "realm/about" <br/>
    ///     Consumers should use <see cref="IDecentralandUrlsSource"/> instead
    /// </summary>
    public class IpfsRealm : IIpfsRealm, IEquatable<IpfsRealm>
    {
        public static readonly URLSubdirectory ENTITIES_ACTIVE_SUBDIR = URLSubdirectory.FromString("entities/active");

        private readonly List<string> sceneUrns;

        public URLDomain EntitiesBaseUrl { get; }
        public URLDomain CatalystBaseUrl { get; }
        public URLDomain ContentBaseUrl { get; }
        public URLDomain LambdasBaseUrl { get; }
        public URLDomain EntitiesActiveEndpoint { get; }

        public IReadOnlyList<string> SceneUrns => sceneUrns;

        public IpfsRealm(URLDomain realmName,
            ServerAbout? serverAbout = null)
        {
            // TODO: realmName resolution, for now just accepts custom realm paths...
            CatalystBaseUrl = realmName;

            if (serverAbout != null)
            {
                sceneUrns = serverAbout.configurations.scenesUrn;
                EntitiesBaseUrl = URLBuilder.Combine(URLDomain.FromString(serverAbout.content.publicUrl), URLSubdirectory.FromString("entities/"));
                ContentBaseUrl = URLDomain.FromString(serverAbout.content.publicUrl);
                LambdasBaseUrl = URLDomain.FromString(serverAbout.lambdas.publicUrl);

                //Note: Content url requires the subdirectory content, but the actives endpoint requires the base one.
                EntitiesActiveEndpoint = URLBuilder.Combine(ContentBaseUrl, ENTITIES_ACTIVE_SUBDIR);
                ContentBaseUrl = URLBuilder.Combine(ContentBaseUrl, URLSubdirectory.FromString("contents/"));
            }
            else
            {
                sceneUrns = new List<string>();
                EntitiesBaseUrl = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/entities/"));
                ContentBaseUrl = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/contents/"));
                EntitiesActiveEndpoint = URLBuilder.Combine(CatalystBaseUrl, URLSubdirectory.FromString("content/entities/active"));
            }
        }

        public bool Equals(IpfsRealm other)
        {
            if (ReferenceEquals(null!, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CatalystBaseUrl == other.CatalystBaseUrl;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null!, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((IpfsRealm)obj);
        }

        public override int GetHashCode() =>
            ContentBaseUrl.GetHashCode();

        public string GetFileHash(byte[] file) =>
            file.IpfsHashV1();
    }
}
