using ECS;

namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl.Current
{
    /// <summary>
    /// TODO why so many abstractions?
    /// </summary>
    public interface ICurrentWorldAboutUrl
    {
        string AboutUrl();

        public static ICurrentWorldAboutUrl NewDefault(IRealmData realmData) =>
            new CurrentWorldAboutUrl(IWorldAboutUrls.DEFAULT, realmData);
    }
}
