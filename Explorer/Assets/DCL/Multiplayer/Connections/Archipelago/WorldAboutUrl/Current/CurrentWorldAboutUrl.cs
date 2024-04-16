using ECS;

namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl.Current
{
    public class CurrentWorldAboutUrl : ICurrentWorldAboutUrl
    {
        private readonly IWorldAboutUrls worldAboutUrls;
        private readonly IRealmData realmData;

        public CurrentWorldAboutUrl(IWorldAboutUrls worldAboutUrls, IRealmData realmData)
        {
            this.worldAboutUrls = worldAboutUrls;
            this.realmData = realmData;
        }

        public string AboutUrl() =>
            worldAboutUrls.AboutUrl(realmData.RealmName);
    }
}
