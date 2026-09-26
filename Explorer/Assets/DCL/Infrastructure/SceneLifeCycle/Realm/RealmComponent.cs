using DCL.Ipfs;

namespace ECS
{
    /// <summary>
    ///     Wraps Realm Data in for ECS, should exist only on one entity at a time.
    /// </summary>
    public readonly struct RealmComponent
    {
        public IIpfsRealm Ipfs => RealmData.Ipfs;
        public IRealmData RealmData { get; }

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        public bool ScenesAreFixed => RealmData.ScenesAreFixed;

        public RealmComponent(IRealmData realmData)
        {
            this.RealmData = realmData;
        }
    }
}
