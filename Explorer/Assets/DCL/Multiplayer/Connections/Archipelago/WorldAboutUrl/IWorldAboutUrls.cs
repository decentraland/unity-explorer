namespace DCL.Multiplayer.Connections.Archipelago.WorldAboutUrl
{
    public interface IWorldAboutUrls
    {
        string AboutUrl(string realmName);

        public static readonly IWorldAboutUrls DEFAULT = new LogWorldAboutUrls(
            new WorldAboutUrls()
        );
    }
}
