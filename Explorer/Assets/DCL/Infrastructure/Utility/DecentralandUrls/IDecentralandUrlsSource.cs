using System.Collections.Generic;

namespace DCL.Multiplayer.Connections.DecentralandUrls
{
    public interface IDecentralandUrlsSource
    {
        /// <summary>
        ///     The Decentraland domain of each environment — the single source of truth for these strings. Anything
        ///     deciding "is this host ours" or hardcoding a fixed-environment endpoint must build on these instead of
        ///     restating the literal; the url templates in <c>DecentralandUrlsSource</c> use the <c>{ENV}</c> token,
        ///     which resolves to the same suffixes.
        /// </summary>
        const string ORG_DOMAIN = "decentraland.org";
        const string ZONE_DOMAIN = "decentraland.zone";
        const string TODAY_DOMAIN = "decentraland.today";

        // IReadOnlyList, not string[]: the array contents would otherwise be writable by any caller, and these gate
        // host-trust checks (SEC-019/020).
        static readonly IReadOnlyList<string> ALL_DOMAINS = new[] { ORG_DOMAIN, ZONE_DOMAIN, TODAY_DOMAIN };

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
        ///     The base domain every backend host of this client sits under: one of <see cref="ORG_DOMAIN" />,
        ///     <see cref="ZONE_DOMAIN" />, <see cref="TODAY_DOMAIN" /> or, for
        ///     <see cref="DecentralandEnvironment.Custom" />, the domain supplied via <c>--base-domain</c>.
        ///     Anything that needs the domain as a value - a host-trust suffix check, a comms hostname - must read
        ///     it here rather than restate a literal, so a custom deployment is not silently compared against
        ///     <c>decentraland.*</c>.
        ///     <para>
        ///         <see cref="DecentralandEnvironment.Today" /> reports <see cref="ORG_DOMAIN" />: it serves a handful
        ///         of endpoints from <see cref="TODAY_DOMAIN" />, which are resolved and pinned while the source is
        ///         built, and everything after that from org.
        ///     </para>
        /// </summary>
        string BaseDomain { get; }

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
