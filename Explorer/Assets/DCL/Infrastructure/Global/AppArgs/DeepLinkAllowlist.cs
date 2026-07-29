using DCL.Multiplayer.Connections.DecentralandUrls;
using System;
using System.Collections.Generic;

namespace Global.AppArgs
{
    /// <summary>
    ///     Deny-by-default allowlist of query params a <c>decentraland://</c> deep link may inject into app-args.
    ///     Shared by the cold-start argv path and the runtime bridge path (both funnel through
    ///     <see cref="ApplicationParametersParser.ProcessDeepLinkParameters" />).
    ///     <para>
    ///     A deep link is fully attacker-controllable — anyone can craft one and get a victim to open it — so
    ///     params fall into three tiers:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <b>Always permitted</b> — benign navigation / share / login intents whose worst case is already
    ///             gated elsewhere (a consent prompt, a matching login token, or a plain coordinate): realm,
    ///             position, community, signin, authRequestId, force-open-backpack, spawnpoint.
    ///         </item>
    ///         <item>
    ///             <b>Permitted only for a whitelisted realm</b> — the local-development params Creator Hub and the
    ///             SDK (<c>sdk-commands</c>) attach to their preview deep links: local-scene, dclenv, hub,
    ///             skip-auth-screen, landscape-terrain-enabled, multi-instance, scene-console. A realm is
    ///             "whitelisted" when it is loopback (127.0.0.1 / localhost / [::1]) OR its world matches the
    ///             <c>deeplink-whitelisted-worlds</c> feature flag (see <see cref="IsRealmWhitelisted" /> and
    ///             <see cref="SetWhitelistedWorlds" />). A remote-realm deep link from a web page can never enable
    ///             them unless that exact world was explicitly whitelisted. Each is individually low-harm — an
    ///             analytics tag, a cosmetic toggle, an instance count, an env enum, a screen skip that still forces
    ///             auth when no valid identity is cached, or the per-scene JS console — and the whitelisted-realm gate
    ///             confines them to the dev context.
    ///         </item>
    ///         <item>
    ///             <b>Never permitted</b> — everything else, in particular params that launch code
    ///             (<c>creator-hub-bin-path</c>, <c>launch-cdp-monitor-on-start</c> — SEC-005); point the client at
    ///             attacker infrastructure (<c>comms-adapter</c>, <c>gatekeeper-url</c>, <c>friends-api-url</c> —
    ///             SEC-052, <c>feature-flags-url</c>/<c>-hostname</c>, <c>optimized-assets-url</c>,
    ///             <c>lsd-remote-ab-server</c>/<c>-world</c>, <c>pulse</c>); bypass a version/specs screen
    ///             (<c>skip-version-check</c>, <c>skip-minimum-specs-screen</c>); or enable other dev/test modes
    ///             (<c>debug</c>, <c>autopilot</c>, <c>alttester</c>, <c>simulate*</c>).
    ///         </item>
    ///     </list>
    ///     Both permitted sets and the world whitelist are a product decision (SEC-019/020 "Design affected") —
    ///     changing them requires sign-off.
    /// </summary>
    public static class DeepLinkAllowlist
    {
        private static readonly HashSet<string> PERMITTED_KEYS = new()
        {
            // Which world/realm to enter. Attacker-controllable, but never applied silently: the switch is routed
            // through the ChangeRealm consent prompt (SEC-004) and a host-suffix environment check.
            AppArgsFlags.REALM,

            // Target parcel "x,y" to land on. A plain coordinate — no security impact.
            AppArgsFlags.POSITION,

            // Community id shown as a "view this community" notification; opening the card is a user action. Benign UI.
            AppArgsFlags.COMMUNITY,

            // Login flow: opaque identity id from the auth website's signin link. Consumed only while a local login
            // is actively awaiting one AND AUTH_REQUEST_ID matches the request that minted it (see DeepLinkHandle).
            AppArgsFlags.SIGNIN,

            // Login flow: binds a signin link to the login that requested it; inert without a matching pending login.
            AppArgsFlags.AUTH_REQUEST_ID,

            // Opens the user's own backpack panel on landing (shipped deep-link feature). Benign in-client navigation.
            AppArgsFlags.FORCE_OPEN_BACKPACK,

            // Named spawn point within the destination scene (#9369). A landing refinement in the same class as
            // POSITION — it only picks where inside an already-permitted realm/position navigation the user arrives,
            // with no capability, infra, or exec impact.
            AppArgsFlags.SPAWN_POINT,
        };

        // Local-development params Creator Hub / sdk-commands attach to preview deep links. Permitted ONLY when the
        // target realm is whitelisted — loopback OR a world configured in the deeplink-whitelisted-worlds feature flag
        // (see IsRealmWhitelisted). A remote-realm deep link can never enable them unless that world was explicitly
        // whitelisted. The SEC-005 exec params (creator-hub-bin-path, launch-cdp-monitor-on-start) are deliberately
        // NOT here; they stay dropped for every realm.
        private static readonly HashSet<string> WHITELISTED_REALM_PERMITTED_KEYS = new()
        {
            // Enables local-scene-development mode (opens an LSD websocket to the realm). Only meaningful against a
            // local/dev server; whitelisted-realm-gated so an attacker can't point LSD at an arbitrary remote realm (SEC-020).
            AppArgsFlags.LOCAL_SCENE,

            // Target environment (org/zone/today). A DCL-owned enum, not a URL — cannot point at attacker infra.
            AppArgsFlags.ENVIRONMENT,

            // Marks the session as launched from the Creator Hub (analytics trait only — no capability unlock).
            AppArgsFlags.DCL_EDITOR,

            // Skips the login screen. Cannot bypass authentication: auth is still forced when no valid identity is
            // cached (RealUserInAppInitializationFlow). A convenience for the local-dev loop.
            AppArgsFlags.SKIP_AUTH_SCREEN,

            // Toggles landscape terrain rendering. Cosmetic.
            AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED,

            // Allows multiple client instances (local multi-instance dev workflow).
            AppArgsFlags.MULTIPLE_RUNNING_INSTANCES,

            // Opens the per-scene JS console (dev tooling for inspecting a scene under development).
            AppArgsFlags.SCENE_CONSOLE,
        };

        // Canonical (lowercased world-name) whitelist, set from the deeplink-whitelisted-worlds feature flag. Empty
        // means loopback-only — the safe default when feature flags are unavailable (e.g. before they are fetched).
        private static HashSet<string> whitelistedWorlds = new();

        public static bool IsPermitted(string key) =>
            PERMITTED_KEYS.Contains(key);

        public static bool IsPermittedForWhitelistedRealm(string key) =>
            WHITELISTED_REALM_PERMITTED_KEYS.Contains(key);

        /// <summary>
        ///     Sets the trusted worlds from the <c>deeplink-whitelisted-worlds</c> feature flag. Entries are accepted
        ///     in full form (a worlds-content-server URL) or short form (a bare ENS name); both are normalized to the
        ///     world name. Passing null / empty resets to loopback-only.
        /// </summary>
        public static void SetWhitelistedWorlds(IEnumerable<string>? worlds)
        {
            var set = new HashSet<string>();

            if (worlds != null)
                foreach (string world in worlds)
                    if (!string.IsNullOrWhiteSpace(world))
                        set.Add(ExtractWorldName(world));

            whitelistedWorlds = set;
        }

        /// <summary>
        ///     Whether the realm a deep link targets is trusted enough to accept the whitelisted-realm dev params:
        ///     loopback, or a world listed in the <c>deeplink-whitelisted-worlds</c> feature flag.
        /// </summary>
        public static bool IsRealmWhitelisted(string? realm)
        {
            if (string.IsNullOrEmpty(realm))
                return false;

            // Only a web-scheme realm can be trusted here: Uri.IsLoopback is true for any file:/// URI (its host is
            // empty), which would otherwise skip both the host check and the consent prompt.
            if (Uri.TryCreate(realm, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                if (uri.IsLoopback)
                    return true;

                // A world name only carries trust when a Decentraland-owned server hosts it. Without this check
                // https://evil.example/world/<whitelisted-world>.dcl.eth would inherit that world's trust, because the
                // name is read from the path — handing an attacker the dev params and (worse) a consent-free realm
                // switch. Uri.Host is the parsed host, so userinfo ("https://x.decentraland.org@evil.example") and port
                // tricks cannot spoof it.
                if (!IsDecentralandHost(uri.Host))
                    return false;
            }

            return whitelistedWorlds.Count > 0 && whitelistedWorlds.Contains(ExtractWorldName(realm));
        }

        // A subdomain of a Decentraland domain (worlds-content-server.decentraland.org, ...). The '.' boundary check
        // is what rejects lookalikes such as "decentraland.org.attacker.com" and "evil-decentraland.org".
        private static bool IsDecentralandHost(string host)
        {
            // Indexed loop, not foreach: enumerating the IReadOnlyList would allocate an enumerator.
            IReadOnlyList<string> domains = IDecentralandUrlsSource.ALL_DOMAINS;

            for (var i = 0; i < domains.Count; i++)
            {
                string domain = domains[i];

                if (host.Length > domain.Length
                    && host[host.Length - domain.Length - 1] == '.'
                    && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        // A world realm is either the bare ENS name (e.g. "myworld.dcl.eth") or a worlds-content-server URL whose
        // last path segment is that ENS. We reduce both the configured entries and the realm to that name and match
        // exactly (case-insensitive) — the exact-membership check, not the shape of the name, is the trust boundary.
        private static string ExtractWorldName(string value)
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                string path = uri.AbsolutePath.Trim('/');
                int lastSlash = path.LastIndexOf('/');
                string segment = lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
                return segment.ToLowerInvariant();
            }

            // Not an http(s) URL — treat the whole value as a bare world name.
            return value.ToLowerInvariant();
        }
    }
}
