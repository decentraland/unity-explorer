namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        const string EXPLORER_LATEST_RELEASE_URL = "https://api.github.com/repos/decentraland/unity-explorer/releases/latest";
        const string LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher-rust";
        const string LEGACY_LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher/dcl";

        string DecentralandDomain { get; }
        DecentralandEnvironment Environment { get; }

        string Url(DecentralandUrl decentralandUrl);
        string GetHostnameForFeatureFlag();
    }
}
