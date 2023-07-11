using Ipfs;

namespace ECS.SceneLifeCycle.Components
{
    /// <summary>
    ///     Realm in ECS, should exist only on one entity at a time
    /// </summary>
    public struct RealmComponent
    {
        public readonly IIpfsRealm Ipfs;

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        public readonly bool ScenesAreFixed;

        public RealmComponent(IIpfsRealm realm)
        {
            ScenesAreFixed = realm.SceneUrns is { Count: > 0 };
            Ipfs = realm;
        }
    }
}
