namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        const string EXPLORER_LATEST_RELEASE_URL = "https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/releases/latest.json";
        const string LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher-rust";
        const string LEGACY_LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher/dcl";

        string DecentralandDomain { get; }
        DecentralandEnvironment Environment { get; }

        string Url(DecentralandUrl decentralandUrl);
        string GetHostnameForFeatureFlag();

        bool RequiresAboutOverride();
    }
}
