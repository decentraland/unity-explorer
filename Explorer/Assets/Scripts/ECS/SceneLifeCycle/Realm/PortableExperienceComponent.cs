using DCL.Ipfs;

namespace ECS
{
    public readonly struct PortableExperienceComponent
    {
        public IIpfsRealm Ipfs => RealmData.Ipfs;
        public IRealmData RealmData { get; }

        //These are the PX created by DCL and cannot be killed or turned off by scenes.
        public bool IsGlobalPortableExperience { get; }

        public string ParentSceneId { get; }

        public PortableExperienceComponent(IRealmData realmData, string parentSceneId, bool isGlobalPortableExperience)
        {
            RealmData = realmData;
            ParentSceneId = parentSceneId;
            IsGlobalPortableExperience = isGlobalPortableExperience;
        }
    }
}
