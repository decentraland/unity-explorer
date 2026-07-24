namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        const string EXPLORER_LATEST_RELEASE_URL = "https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/releases/latest.json";
        const string LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher-rust";
        const string LEGACY_LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher/dcl";

        /// <summary>
        ///     Local MCP server endpoint template; {0} is the port (see "--mcp-port"). Docs and scripts restate the
        ///     path (they cannot reference this const): docs/mcp-automation.md, docs/app-arguments.md,
        ///     .claude/skills/mcp-scene-iteration/ — keep in sync.
        /// </summary>
        const string LOCAL_MCP_ENDPOINT_URL = "http://127.0.0.1:{0}/unity-explorer-mcp";

        /// <summary>
        ///     Get a raw url without caching at any moment (without dependency on FF)
        /// </summary>
        public string Probe(DecentralandUrl decentralandUrl);

        string Url(DecentralandUrl decentralandUrl);

        public string TransformUrl(string originalUrl);

        /// <summary>
        ///     Only used by Signed Fetch, as the original URL should be signed (not the gateway one). <br />
        ///     It's an expensive allocating function that shouldn't be used frequently
        /// </summary>
        public string GetOriginalUrl(string url);

        string GetHostnameForFeatureFlag();
    }
}
