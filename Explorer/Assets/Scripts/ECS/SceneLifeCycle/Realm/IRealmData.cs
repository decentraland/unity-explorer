#nullable enable

using CommunicationData.URLHelpers;
using DCL.Ipfs;

namespace ECS
{
    /// <summary>
    ///     Readonly interface to fetch realm data
    /// </summary>
    public interface IRealmData
    {
        IIpfsRealm Ipfs { get; }

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        bool ScenesAreFixed { get; }

        /// <summary>
        ///     Name of the realm
        /// </summary>
        string RealmName { get; }

        /// <summary>
        ///     Whether the data was set at least once
        /// </summary>
        bool Configured { get; }

        class Fake : IRealmData
        {
            public Fake(string realmName = "baldr") : this(
                new LocalIpfsRealm(new URLDomain()),
                true,
                realmName,
                true
            ) { }

            public Fake(IIpfsRealm ipfs, bool scenesAreFixed, string realmName, bool configured)
            {
                Ipfs = ipfs;
                ScenesAreFixed = scenesAreFixed;
                RealmName = realmName;
                Configured = configured;
            }

            public IIpfsRealm Ipfs { get; }
            public bool ScenesAreFixed { get; }
            public string RealmName { get; }
            public bool Configured { get; }
        }
    }
}
