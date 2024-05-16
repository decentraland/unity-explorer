#nullable enable

using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
        int NetworkId { get; }
        string CommsAdapter { get; }

        /// <summary>
        ///     Whether the data was set at least once
        /// </summary>
        bool Configured { get; }
        bool IsDirty { get; }

        class Fake : IRealmData
        {
            public IIpfsRealm Ipfs { get; }
            public bool ScenesAreFixed { get; }
            public string RealmName { get; }
            public int NetworkId { get; }
            public string CommsAdapter { get; }
            public bool Configured { get; }
            public bool IsDirty { get; internal set; }

            public Fake(int networkId = 1, string commsAdapter = "", string realmName = "baldr") : this(
                new LocalIpfsRealm(new URLDomain()),
                true,
                realmName,
                true, networkId, commsAdapter) { }

            public Fake(IIpfsRealm ipfs, bool scenesAreFixed, string realmName, bool configured, int networkId,
                string commsAdapter)
            {
                Ipfs = ipfs;
                ScenesAreFixed = scenesAreFixed;
                RealmName = realmName;
                Configured = configured;
                NetworkId = networkId;
                CommsAdapter = commsAdapter;
            }
        }
    }

    public static class RealmDataExtensions
    {
        public static UniTask WaitConfiguredAsync(this IRealmData realmData) =>
            UniTask.WaitUntil(() => realmData.Configured);
    }
}
