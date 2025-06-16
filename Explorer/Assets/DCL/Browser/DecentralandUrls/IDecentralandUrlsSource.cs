using System;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        static readonly Uri EXPLORER_LATEST_RELEASE_URL = new ("https://api.github.com/repos/decentraland/unity-explorer/releases/latest");
        static readonly Uri LAUNCHER_DOWNLOAD_URL = new ("https://explorer-artifacts.decentraland.org/launcher-rust");
        static readonly Uri LEGACY_LAUNCHER_DOWNLOAD_URL = new ("https://explorer-artifacts.decentraland.org/launcher/dcl");

        string DecentralandDomain { get; }
        DecentralandEnvironment Environment { get; }

        Uri Url(DecentralandUrl decentralandUrl);
        string GetHostnameForFeatureFlag();
    }
}
