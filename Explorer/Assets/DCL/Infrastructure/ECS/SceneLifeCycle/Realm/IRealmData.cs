using CommunicationData.URLHelpers;
using DCL.Ipfs;
using DCL.Utilities;
using System;
using Unity.Collections;
using Unity.Mathematics;

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
        ///     Whether the data was set at least once
        /// </summary>
        bool Configured { get; }
        bool IsDirty { get; }

        /// <summary>
        ///     Road parcels from WorldManifest.json
        /// </summary>
        NativeParallelHashSet<int2> RoadParcels { get; }

        /// <summary>
        ///     Occupied parcels from WorldManifest.json
        /// </summary>
        NativeParallelHashSet<int2> OccupiedParcels { get; }

        /// <summary>
        ///     Empty parcels from WorldManifest.json
        /// </summary>
        NativeParallelHashSet<int2> EmptyParcels { get; }

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
            public bool Configured { get; }
            public bool IsDirty { get; internal set; }

            public NativeParallelHashSet<int2> RoadParcels { get; }
            public NativeParallelHashSet<int2> OccupiedParcels { get; }
            public NativeParallelHashSet<int2> EmptyParcels { get; }

            public Fake(int networkId = 1, string commsAdapter = "", string realmName = "baldr", string protocol = "v3",
                string hostname = "realm-provider.decentraland.org") : this(
                new LocalIpfsRealm(new URLDomain()),
                true,
                realmName,
                true, networkId, commsAdapter, protocol, hostname,
                new NativeParallelHashSet<int2>(),
                new NativeParallelHashSet<int2>(),
                new NativeParallelHashSet<int2>()) { }

            public Fake(IIpfsRealm ipfs, bool scenesAreFixed, string realmName, bool configured, int networkId,
                string commsAdapter, string protocol, string hostname, NativeParallelHashSet<int2> roadParcels,
                NativeParallelHashSet<int2> occupiedParcels, NativeParallelHashSet<int2> emptyParcels)
            {
                Ipfs = ipfs;
                ScenesAreFixed = scenesAreFixed;
                RealmName = realmName;
                Configured = configured;
                NetworkId = networkId;
                CommsAdapter = commsAdapter;
                Protocol = protocol;
                Hostname = hostname;
                RoadParcels = roadParcels;
                OccupiedParcels = occupiedParcels;
                EmptyParcels = emptyParcels;
            }
        }
    }
}
