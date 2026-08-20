using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace Global.Dynamic
{
    /// <summary>
    ///     Host policy for the startup trusted-realm gate: which realm hosts may be entered without the
    ///     untrusted-realm consent prompt. Consulted by <c>MainSceneLoader.IsTrustedRealmAsync</c> before it falls
    ///     back to the catalyst <c>lambdas/contracts/servers</c> lookup, so a match here also spares that request.
    ///     <para>
    ///         A realm reaching this policy is attacker-influenced by design: <c>realm</c> is one of the query params
    ///         a <c>decentraland://</c> deep link may inject, and it sits on <c>DeepLinkAllowlist</c>'s
    ///         always-permitted tier *because* this gate exists to catch it. Trusting a host here therefore removes a
    ///         consent prompt for every user, not only for automation, so an entry must name infrastructure
    ///         Decentraland controls end to end.
    ///     </para>
    /// </summary>
    public static class TrustedRealms
    {
        /// <summary>
        ///     Hosts trusted verbatim, scheme-agnostic. Only two kinds of host belong here: loopback, which sits
        ///     under no Decentraland domain and is served over plain http by local scene development and by a locally
        ///     hosted E2E fixture; and individual production hosts, which have to be named one by one because
        ///     <see cref="IDecentralandUrlsSource.ORG_DOMAIN" /> is intentionally not in <see cref="TRUSTED_DOMAINS" />.
        ///     <para>
        ///         A <see cref="IDecentralandUrlsSource.ZONE_DOMAIN" /> host never belongs here — the whole domain is
        ///         already trusted below, so naming one of its hosts would only be dead weight that reads as if the
        ///         rest of the domain were untrusted.
        ///     </para>
        /// </summary>
        private static readonly string[] TRUSTED_HOSTS =
        {
            "127.0.0.1",
            "localhost",
            "sdk-team-cdn." + IDecentralandUrlsSource.ORG_DOMAIN,
            "realm-provider-ea." + IDecentralandUrlsSource.ORG_DOMAIN,
            "worlds-content-server." + IDecentralandUrlsSource.ORG_DOMAIN,
        };

        /// <summary>
        ///     Domains whose every subdomain is Decentraland-controlled, and so are trusted without being named.
        ///     <para>
        ///         <see cref="IDecentralandUrlsSource.ZONE_DOMAIN" /> is the non-production environment. It hosts the
        ///         ephemeral E2E Catalyst fixtures at <c>f-{id}.e2e-fixtures.decentraland.zone</c>, whose hostname is
        ///         minted per run and therefore cannot be enumerated in <see cref="TRUSTED_HOSTS" />.
        ///     </para>
        ///     <para>
        ///         <see cref="IDecentralandUrlsSource.ORG_DOMAIN" /> is deliberately absent. Production keeps
        ///         per-host entries so that one production subdomain — or one dangling DNS record under it — cannot
        ///         become a silent realm switch for every user.
        ///     </para>
        /// </summary>
        private static readonly string[] TRUSTED_DOMAINS =
        {
            IDecentralandUrlsSource.ZONE_DOMAIN,
        };

        /// <summary>
        ///     True when <paramref name="realm" /> may be entered without asking the user to confirm it.
        ///     Callers pass an already-parsed absolute uri, so malformed-realm handling stays with the caller.
        /// </summary>
        public static bool IsTrusted(Uri realm)
        {
            string host = realm.Host;

            foreach (string trustedHost in TRUSTED_HOSTS)
                if (string.Equals(host, trustedHost, StringComparison.OrdinalIgnoreCase))
                    return true;

            // Domain-level trust is https-only: a remote realm reached over cleartext can be answered by a network
            // attacker, so inheriting the domain's trust would hand out that trust to anyone on the path. The exact
            // hosts above opt out of this because loopback has no meaningful network to attack.
            if (!string.Equals(realm.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (string domain in TRUSTED_DOMAINS)
                if (IsWithinDomain(host, domain))
                    return true;

            return false;
        }

        /// <summary>
        ///     Suffix match anchored to a label boundary: the domain itself, or any host strictly below it. Requiring
        ///     the dot is what keeps a look-alike registration such as <c>evildecentraland.zone</c> out, and requiring
        ///     the suffix to be terminal is what keeps <c>decentraland.zone.example.com</c> out.
        /// </summary>
        private static bool IsWithinDomain(string host, string domain) =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
            || (host.Length > domain.Length + 1
                && host[host.Length - domain.Length - 1] == '.'
                && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
    }
}
