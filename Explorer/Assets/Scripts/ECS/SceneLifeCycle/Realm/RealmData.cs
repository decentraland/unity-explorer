using DCL.Ipfs;
using System;

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

        public string RealmName { get; private set; }
        public int NetworkId{ get; private set; }
        public string CommsAdapter { get; private set; }
        public string Protocol { get; private set; }
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

        public RealmData(IIpfsRealm ipfsRealm)
        {
            Reconfigure(ipfsRealm, string.Empty, DEFAULT_NETWORK_ID, string.Empty, string.Empty);
        }

        public void Reconfigure(IIpfsRealm ipfsRealm, string realmName, int networkId, string commsAdapter, string protocol)
        {
            IsDirty = true;
            Configured = true;

            RealmName = realmName;
            scenesAreFixed = ipfsRealm.SceneUrns is { Count: > 0 };
            ipfs = ipfsRealm;
            CommsAdapter = commsAdapter;
            Protocol = protocol;
            NetworkId = networkId;
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

        public bool IsDirty { get; internal set; } = true;
    }
}
