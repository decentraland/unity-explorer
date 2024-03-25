using DCL.Ipfs;
using Ipfs;

namespace ECS
{
    /// <summary>
    ///     Wraps Realm Data in for ECS, should exist only on one entity at a time.
    /// </summary>
    public readonly struct RealmComponent
    {
        public IIpfsRealm Ipfs => realmData.Ipfs;
        public IRealmData RealmData => realmData;

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        public bool ScenesAreFixed => realmData.ScenesAreFixed;

        private readonly IRealmData realmData;

        public RealmComponent(IRealmData realmData)
        {
            this.realmData = realmData;
        }
    }
}
