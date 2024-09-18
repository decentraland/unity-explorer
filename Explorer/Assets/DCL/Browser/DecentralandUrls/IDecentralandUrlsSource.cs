namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        const string EXPLORER_LATEST_RELEASE_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
        const string LAUNCHER_LATEST_RELEASE_URL = "https://api.github.com/repos/decentraland/launcher/releases/latest";
        const string LAUNCHER_DOWNLOAD_URL = "https://github.com/decentraland/launcher/releases/download";

        string DecentralandDomain { get; }

        string Url(DecentralandUrl decentralandUrl);
    }
}
