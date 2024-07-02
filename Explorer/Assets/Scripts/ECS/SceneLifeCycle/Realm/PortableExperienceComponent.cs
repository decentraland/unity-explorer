using DCL.Ipfs;

namespace ECS
{
    public readonly struct PortableExperienceComponent
    {
        public IIpfsRealm Ipfs => RealmData.Ipfs;
        public IRealmData RealmData { get; }

        public PortableExperienceComponent(IRealmData realmData)
        {
            this.RealmData = realmData;
        }
    }
}
