using DCL.Ipfs;
using System;

namespace ECS
{
    /// <summary>
    ///     Reference data that is retained in a single instance
    /// </summary>
    public class RealmData : IRealmData
    {
        private IIpfsRealm ipfs;
        private bool scenesAreFixed;

        public string RealmName { get; private set; }

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

        /// <summary>
        ///     Create an empty data to configure later
        /// </summary>
        public RealmData() { }

        public RealmData(IIpfsRealm ipfsRealm)
        {
            Reconfigure(ipfsRealm, string.Empty);
        }

        public void Reconfigure(IIpfsRealm ipfsRealm, string realmName)
        {
            Configured = true;

            RealmName = realmName;
            scenesAreFixed = ipfsRealm.SceneUrns is { Count: > 0 };
            ipfs = ipfsRealm;
        }

        /// <summary>
        ///     Make the data invalid (forbidding access to the URLs)
        /// </summary>
        public void Invalidate()
        {
            Configured = false;
            ipfs = null;
        }

        private void Validate()
        {
            if (!Configured)
                throw new InvalidOperationException("RealmData has not been configured");
        }
    }
}
