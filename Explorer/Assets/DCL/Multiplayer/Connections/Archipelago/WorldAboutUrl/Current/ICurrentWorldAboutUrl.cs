using ECS;

namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl.Current
{
    public interface ICurrentWorldAboutUrl
    {
        string AboutUrl();

        public static ICurrentWorldAboutUrl NewDefault(IRealmData realmData) =>
            new CurrentWorldAboutUrl(IWorldAboutUrls.DEFAULT, realmData);
    }
}
