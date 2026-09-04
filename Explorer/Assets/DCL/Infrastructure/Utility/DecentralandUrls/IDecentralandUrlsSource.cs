using System;
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

        // IReadOnlyList, not string[]: the array contents would otherwise be writable by any caller, and these gate
        // host-trust checks (SEC-019/020).
        static readonly IReadOnlyList<string> ALL_DOMAINS = new[] { ORG_DOMAIN, ZONE_DOMAIN };

        /// <summary>
        ///     Whether <paramref name="host" /> sits strictly below <paramref name="domain" />
        ///     ("worlds-content-server.decentraland.org" under "decentraland.org"). The '.' boundary check is what
        ///     rejects lookalikes such as "decentraland.org.attacker.com" and "evil-decentraland.org". Pass a parsed
        ///     host — this does no url parsing, so a caller handing it an authority could be spoofed through userinfo.
        /// </summary>
        static bool IsSubdomainOf(string host, string domain) =>
            host.Length > domain.Length
            && host[host.Length - domain.Length - 1] == '.'
            && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///     <see cref="IsSubdomainOf" />, plus the domain itself. Use this where the registrable domain is a host in
        ///     its own right; prefer <see cref="IsSubdomainOf" /> where only subdomains should carry trust.
        /// </summary>
        static bool IsHostWithinDomain(string host, string domain) =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase) || IsSubdomainOf(host, domain);

        const string EXPLORER_LATEST_RELEASE_URL = "https://explorer-artifacts.decentraland.org/@dcl/unity-explorer/releases/latest.json";
        const string LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher-rust";
        const string LEGACY_LAUNCHER_DOWNLOAD_URL = "https://explorer-artifacts.decentraland.org/launcher/dcl";

        /// <summary>
        ///     Local MCP server endpoint template; {0} is the port (see "--mcp-port"). Docs and scripts restate the
        ///     path (they cannot reference this const): docs/mcp-automation.md, docs/app-arguments.md, and the
        ///     unity-explorer-mcp skill in the sdk-skills repo — keep in sync.
        /// </summary>
        const string LOCAL_MCP_ENDPOINT_URL = "http://127.0.0.1:{0}/unity-explorer-mcp";

        /// <summary>
        ///     The base domain every backend host of this client sits under: one of <see cref="ORG_DOMAIN" />,
        ///     <see cref="ZONE_DOMAIN" /> or, for
        ///     <see cref="DecentralandEnvironment.Custom" />, the domain supplied via <c>--base-domain</c>.
        ///     Anything that needs the domain as a value - a host-trust suffix check, a comms hostname - must read
        ///     it here rather than restate a literal, so a custom deployment is not silently compared against
        ///     <c>decentraland.*</c>.
        /// </summary>
        string BaseDomain { get; }

        /// <summary>
        ///     The single origin every supported service is routed through when <c>--gateway</c> named one (or the
        ///     <c>use-gateway</c> flag derived <c>gateway.{BaseDomain}</c>), including the trailing '/'; null when
        ///     gateway routing is off. A host-trust check must accept it alongside <see cref="BaseDomain" />: a
        ///     gateway origin is deliberately allowed to sit outside the base domain — an e2e fixture's is loopback —
        ///     so comparing only against the domain would reject this session's own realms. Naming one is
        ///     command-line only (<c>DeepLinkAllowlist</c> denies <c>--gateway</c>), so trusting it adds no
        ///     link-reachable surface. Keep the trailing '/' when comparing: it is the boundary that stops
        ///     "http://127.0.0.1:8080.attacker.com/" from matching the origin "http://127.0.0.1:8080/".
        /// </summary>
        string? GatewayOrigin { get; }

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
