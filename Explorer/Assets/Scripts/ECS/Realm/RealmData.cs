using Ipfs;
using System;

namespace ECS
{
    /// <summary>
    ///     Reference data that is retained in a single instance
    /// </summary>
    public class RealmData : IRealmData
    {
        private bool configured;

        private IIpfsRealm ipfs;
        private bool scenesAreFixed;

        /// <summary>
        ///     Create an empty data to configure later
        /// </summary>
        public RealmData() { }

        public RealmData(IIpfsRealm ipfsRealm)
        {
            Reconfigure(ipfsRealm);
        }

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

        public void Reconfigure(IIpfsRealm ipfsRealm)
        {
            configured = true;

            scenesAreFixed = ipfsRealm.SceneUrns is { Count: > 0 };
            ipfs = ipfsRealm;
        }

        private void Validate()
        {
            if (!configured)
                throw new InvalidOperationException("RealmData has not been configured");
        }
    }
}
