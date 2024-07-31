namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        string DecentralandDomain { get; }

        string Url(DecentralandUrl decentralandUrl);
    }
}
