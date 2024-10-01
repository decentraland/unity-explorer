using DCL.Ipfs;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Utility;

namespace ECS
{
    /// <summary>
    ///     Reference data that is retained in a single instance
    /// </summary>
    public class RealmData : IRealmData
    {
        private const int DEFAULT_NETWORK_ID = 1;

        private IIpfsRealm ipfs = InvalidIpfsRealm.Instance;
        private bool scenesAreFixed;
        private List<int2>? localSceneParcels;

        public string RealmName { get; private set; }
        public int NetworkId{ get; private set; }
        public string CommsAdapter { get; private set; }
        public string Protocol { get; private set; }
        public string Hostname { get; private set; }
        public bool Configured { get; private set; }

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

        public IReadOnlyList<int2>? LocalSceneParcels
        {
            get
            {
                Validate();
                return localSceneParcels;
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
            Reconfigure(ipfsRealm, string.Empty, DEFAULT_NETWORK_ID, string.Empty, string.Empty, string.Empty, null);
        }

        public void Reconfigure(IIpfsRealm ipfsRealm, string realmName, int networkId, string commsAdapter, string protocol, string hostname, IReadOnlyList<string>? localRealmSceneParcels = null)
        {
            IsDirty = true;
            Configured = true;
            RealmName = realmName;
            ipfs = ipfsRealm;
            CommsAdapter = commsAdapter;
            Protocol = protocol;
            NetworkId = networkId;
            Hostname = hostname;
            scenesAreFixed = ipfsRealm.SceneUrns is { Count: > 0 };
            if (localRealmSceneParcels is { Count: > 0 })
            {
                scenesAreFixed = true;
                localSceneParcels = new List<int2>();
                foreach (string parcel in localRealmSceneParcels)
                {
                    localSceneParcels.Add(IpfsHelper.DecodePointer(parcel).ToInt2());
                }
            }
        }

        /// <summary>
        ///     Make the data invalid (forbidding access to the URLs)
        /// </summary>
        public void Invalidate()
        {
            Configured = false;
            ipfs = InvalidIpfsRealm.Instance;
        }

        private void Validate()
        {
            if (!Configured)
                throw new InvalidOperationException("RealmData has not been configured");
        }

        public bool IsDirty { get; set; } = true;
    }
}
