using DCL.Ipfs;

namespace ECS
{
    public readonly struct PortableExperienceComponent
    {
        public IIpfsRealm Ipfs => realmData.Ipfs;

        /// <summary>
        ///     Indicates that the realm contains a fixed number of scenes
        /// </summary>
        public bool ScenesAreFixed => realmData.ScenesAreFixed;

        private readonly IRealmData realmData;

        public PortableExperienceComponent(IRealmData realmData)
        {
            this.realmData = realmData;
        }
    }
}
