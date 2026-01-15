using DCL.Ipfs;
using DCL.Utilities;
using System;
using System.Text;

namespace ECS
{
    /// <summary>
    ///     Reference data that is retained in a single instance
    /// </summary>
    public class RealmData : IRealmData
    {
        private const int DEFAULT_NETWORK_ID = 1;

        private readonly ReactiveProperty<RealmKind> realmType = new (RealmKind.Uninitialized);

        private IIpfsRealm ipfs = InvalidIpfsRealm.Instance;
        private bool scenesAreFixed;

        public string RealmName { get; private set; }
        public int NetworkId { get; private set; }
        public string CommsAdapter { get; private set; }
        public string Protocol { get; private set; }
        public string Hostname { get; private set; }
        public bool IsLocalSceneDevelopment { get; private set; }
        public bool Configured { get; private set; }

        public IReadonlyReactiveProperty<RealmKind> RealmType => realmType;

        public IIpfsRealm Ipfs
        {
            get
            {
                Validate();
                return ipfs;
            }
        }

        public bool ScenesAreFixed
        {
            get
            {
                Validate();
                return scenesAreFixed;
            }
        }

        /// <summary>
        /// Create an empty data to configure later
        /// </summary>
        public RealmData()
        {
            RealmName = string.Empty;
            CommsAdapter = string.Empty;
            Protocol = string.Empty;
            Hostname = string.Empty;
        }

        public RealmData(IIpfsRealm ipfsRealm)
        {
            Reconfigure(ipfsRealm, string.Empty, DEFAULT_NETWORK_ID, string.Empty, string.Empty, string.Empty, false);
        }

        public void Reconfigure(IIpfsRealm ipfsRealm, string realmName, int networkId, string commsAdapter, string protocol,
            string hostname, bool isLocalSceneDevelopment)
        {
            IsDirty = true;
            Configured = true;
            RealmName = realmName;
            scenesAreFixed = ipfsRealm.SceneUrns is { Count: > 0 };
            ipfs = ipfsRealm;
            CommsAdapter = commsAdapter;
            Protocol = protocol;
            NetworkId = networkId;
            Hostname = hostname;
            IsLocalSceneDevelopment = isLocalSceneDevelopment;

            if (isLocalSceneDevelopment)
                realmType.Value = RealmKind.LocalScene;
            else if (!scenesAreFixed)
                realmType.Value = RealmKind.GenesisCity;
            else
                realmType.Value = RealmKind.World;
        }

        /// <summary>
        ///     Make the data invalid (forbidding access to the URLs)
        /// </summary>
        public void Invalidate()
        {
            Configured = false;
            ipfs = InvalidIpfsRealm.Instance;
            realmType.Value = RealmKind.Uninitialized;
        }

        private void Validate()
        {
            if (!Configured)
                throw new InvalidOperationException("RealmData has not been configured");
        }

        public bool IsDirty { get; set; } = true;

        public override string ToString()
        {
            if (!Configured)
                return "[RealmData: Not Configured]";

            var sb = new StringBuilder();
            sb.AppendLine("[RealmData Snapshot]");
            sb.AppendLine($"  - Realm Name: {RealmName}");
            sb.AppendLine($"  - Network ID: {NetworkId}");
            sb.AppendLine($"  - Realm Type: {RealmType.Value}");
            sb.AppendLine($"  - Hostname: {Hostname}");
            sb.AppendLine($"  - Protocol: {Protocol}");
            sb.AppendLine($"  - Comms Adapter: {CommsAdapter}");
            sb.AppendLine("  - IPFS Endpoints:");
            sb.AppendLine($"    - Content URL: {Ipfs.ContentBaseUrl}");
            sb.AppendLine($"    - Lambdas URL: {Ipfs.LambdasBaseUrl}");
            sb.AppendLine($"    - Scene URNs: {(Ipfs.SceneUrns is { Count: > 0 } ? string.Join(", ", Ipfs.SceneUrns) : "N/A")}");
            sb.AppendLine("  - Flags:");
            sb.AppendLine($"    - Is Local Scene Dev: {IsLocalSceneDevelopment}");
            sb.AppendLine($"    - Scenes Are Fixed: {ScenesAreFixed}");
            sb.AppendLine($"    - Is Dirty: {IsDirty}");
            return sb.ToString();
        }
    }
}
