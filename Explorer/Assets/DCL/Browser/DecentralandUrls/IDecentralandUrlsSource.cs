namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        DecentralandEnvironment Environment { get; }

        string Url(DecentralandUrl decentralandUrl);
    }
}
