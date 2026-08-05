using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.Utilities;

namespace ECS
{
    /// <summary>
    ///     Readonly interface to fetch realm data
    /// </summary>
    public interface IRealmData
    {
        IIpfsRealm Ipfs { get; }

        IReadonlyReactiveProperty<RealmKind> RealmType { get; }

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
        string Protocol { get; }
        string Hostname { get; }
        bool IsLocalSceneDevelopment { get; }

        /// <summary>
        ///     Access secret for the currently configured private world; empty unless the realm it was validated for is configured.
        /// </summary>
        string WorldCommsSecret { get; }

        /// <summary>
        ///     Whether the data was set at least once
        /// </summary>
        bool Configured { get; }
        bool IsDirty { get; }

        /// <summary>
        ///     World manifest that describes the world state
        /// </summary>
        WorldManifest WorldManifest { get; }
        bool SingleScene { get; }

        /// <summary>
        ///     Realm-level fixed skybox hour in seconds (from server about configurations.skybox.fixedHour).
        ///     Null when not set, meaning the realm does not enforce a fixed time of day.
        ///     Measured in seconds of a day
        /// </summary>
        float? SkyboxFixedHour { get; }

        /// <summary>
        ///     Stores a world access secret scoped to the exact realm URL it was validated against.
        ///     It becomes <see cref="WorldCommsSecret" /> only when that same URL is configured, never for a different realm.
        /// </summary>
        void SetPendingWorldCommsSecret(URLDomain validatedRealm, string secret);

        /// <summary>
        ///     Discards a pending secret that was never (or no longer needs to be) applied.
        /// </summary>
        void ClearPendingWorldCommsSecret();

        class Fake : IRealmData
        {
            public IIpfsRealm Ipfs { get; }
            public IReadonlyReactiveProperty<RealmKind> RealmType => new ReactiveProperty<RealmKind>(RealmKind.GenesisCity);
            public bool ScenesAreFixed { get; }
            public string RealmName { get; }
            public int NetworkId { get; }
            public string CommsAdapter { get; }
            public string Protocol { get; }
            public string Hostname { get; }
            public bool IsLocalSceneDevelopment { get; }
            public string WorldCommsSecret { get; set; } = string.Empty;
            public bool Configured { get; }
            public bool IsDirty { get; internal set; }
            public WorldManifest WorldManifest { get; }
            public bool SingleScene { get; }
            public float? SkyboxFixedHour { get; }

            public Fake(int networkId = 1, string commsAdapter = "", string realmName = "baldr", string protocol = "v3",
                string hostname = "realm-provider.decentraland.org") : this(
                new LocalIpfsRealm(new URLDomain()),
                true,
                realmName,
                true, networkId, commsAdapter, protocol, hostname) { }

            public Fake(IIpfsRealm ipfs, bool scenesAreFixed, string realmName, bool configured, int networkId,
                string commsAdapter, string protocol, string hostname)
            {
                Ipfs = ipfs;
                ScenesAreFixed = scenesAreFixed;
                RealmName = realmName;
                Configured = configured;
                NetworkId = networkId;
                CommsAdapter = commsAdapter;
                Protocol = protocol;
                Hostname = hostname;
                WorldManifest = WorldManifest.Empty;
            }

            public void SetPendingWorldCommsSecret(URLDomain validatedRealm, string secret) { }

            public void ClearPendingWorldCommsSecret() { }
        }
    }
}
