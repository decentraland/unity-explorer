using DCL.Multiplayer.Connections.DecentralandUrls;
using System;

namespace Global.Dynamic
{
    /// <summary>
    ///     Realm hosts that may be entered without the untrusted-realm consent prompt. <c>realm</c> is
    ///     deep-link injectable, so an entry here drops that prompt for every user, not only for automation:
    ///     it must name infrastructure Decentraland controls end to end.
    /// </summary>
    public static class TrustedRealms
    {
        /// <summary>
        ///     Trusted verbatim, any scheme. Loopback only, plus production hosts one by one —
        ///     <see cref="IDecentralandUrlsSource.ORG_DOMAIN" /> must never join <see cref="TRUSTED_DOMAINS" />.
        ///     Never add a <see cref="IDecentralandUrlsSource.ZONE_DOMAIN" /> host: the domain already covers it.
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
        ///     Domains trusted down to every subdomain. <see cref="IDecentralandUrlsSource.ZONE_DOMAIN" /> covers
        ///     the E2E fixtures at <c>f-{id}.e2e-fixtures.decentraland.zone</c>, minted per run and so impossible
        ///     to enumerate. Production stays per-host: one subdomain of it — or one dangling DNS record under it
        ///     — would otherwise be a silent realm switch for every user.
        /// </summary>
        private static readonly string[] TRUSTED_DOMAINS =
        {
            IDecentralandUrlsSource.ZONE_DOMAIN,
        };

        /// <summary>True when <paramref name="realm" />, an absolute uri, needs no consent prompt.</summary>
        public static bool IsTrusted(Uri realm)
        {
            string host = realm.Host;

            foreach (string trustedHost in TRUSTED_HOSTS)
                if (string.Equals(host, trustedHost, StringComparison.OrdinalIgnoreCase))
                    return true;

            // Domain trust is https-only: over cleartext a network attacker answers as any host under the
            // domain. The exact hosts opt out — loopback has no meaningful network to attack.
            if (!string.Equals(realm.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (string domain in TRUSTED_DOMAINS)
                if (IsWithinDomain(host, domain))
                    return true;

            return false;
        }

        /// <summary>
        ///     The domain itself, or any host strictly below it. The dot rejects <c>evildecentraland.zone</c>;
        ///     anchoring the suffix to the end rejects <c>decentraland.zone.example.com</c>.
        /// </summary>
        private static bool IsWithinDomain(string host, string domain) =>
            string.Equals(host, domain, StringComparison.OrdinalIgnoreCase)
            || (host.Length > domain.Length + 1
                && host[host.Length - domain.Length - 1] == '.'
                && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase));
    }
}
