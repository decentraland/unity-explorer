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
    ///             position, community, signin, authRequestId, force-open-backpack.
    ///         </item>
    ///         <item>
    ///             <b>Permitted only for a loopback realm</b> — the local-development params Creator Hub and the
    ///             SDK (<c>sdk-commands</c>) attach to their preview deep links: local-scene, dclenv, hub,
    ///             skip-auth-screen, landscape-terrain-enabled, multi-instance. They are gated on
    ///             <c>Uri.IsLoopback</c> of the target realm (127.0.0.1 / localhost / [::1]) so a remote-realm deep
    ///             link from a web page can never enable them, while a legitimate local-dev launch (which always
    ///             targets loopback) works. Each is individually low-harm — an analytics tag, a cosmetic toggle, an
    ///             instance count, an env enum, or a screen skip that still forces auth when no valid identity is
    ///             cached — and the loopback gate confines them to the dev context.
    ///         </item>
    ///         <item>
    ///             <b>Never permitted</b> — everything else, in particular params that launch code
    ///             (<c>creator-hub-bin-path</c>, <c>launch-cdp-monitor-on-start</c> — SEC-005); point the client at
    ///             attacker infrastructure (<c>comms-adapter</c>, <c>gatekeeper-url</c>, <c>friends-api-url</c> —
    ///             SEC-052, <c>feature-flags-url</c>/<c>-hostname</c>, <c>optimized-assets-url</c>,
    ///             <c>lsd-remote-ab-server</c>/<c>-world</c>, <c>pulse</c>); bypass a version/specs screen
    ///             (<c>skip-version-check</c>, <c>skip-minimum-specs-screen</c>); or enable other dev/test modes
    ///             (<c>debug</c>, <c>scene-console</c>, <c>autopilot</c>, <c>alttester</c>, <c>simulate*</c>).
    ///         </item>
    ///     </list>
    ///     Both permitted sets are a product decision (SEC-019/020 "Design affected") — changing them requires sign-off.
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
        };

        // Local-development params Creator Hub / sdk-commands attach to preview deep links. Permitted ONLY when the
        // target realm is loopback (see ApplicationParametersParser.ProcessDeepLinkParameters) — a remote-realm deep
        // link can never enable them. The SEC-005 exec params (creator-hub-bin-path, launch-cdp-monitor-on-start)
        // are deliberately NOT here; they stay dropped for every realm.
        private static readonly HashSet<string> LOOPBACK_REALM_PERMITTED_KEYS = new()
        {
            // Enables local-scene-development mode (opens an LSD websocket to the realm). Only meaningful against a
            // local server; loopback-gated so an attacker can't point LSD at a remote realm (SEC-020).
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
        };

        public static bool IsPermitted(string key) =>
            PERMITTED_KEYS.Contains(key);

        public static bool IsPermittedForLoopbackRealm(string key) =>
            LOOPBACK_REALM_PERMITTED_KEYS.Contains(key);
    }
}
