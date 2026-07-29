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
    ///             <b>Permitted only for a loopback realm</b> — the local-development params Creator Hub and the
    ///             SDK (<c>sdk-commands</c>) attach to their preview deep links: local-scene, dclenv, hub,
    ///             skip-auth-screen, landscape-terrain-enabled, multi-instance, mcp, mcp-port, local-ab.
    ///             They are gated on
    ///             <c>Uri.IsLoopback</c> of the target realm (127.0.0.1 / localhost / [::1]) so a remote-realm deep
    ///             link from a web page can never enable them, while a legitimate local-dev launch (which always
    ///             targets loopback) works. All but the MCP pair are individually low-harm — an analytics tag, a
    ///             cosmetic toggle, an instance count, an env enum, a screen skip that still forces auth when no
    ///             valid identity is cached, or an asset-server toggle whose base URL derives from the realm this
    ///             gate already checked; <c>mcp</c>/<c>mcp-port</c> start an unauthenticated loopback control
    ///             port, so they lean on the gate plus the server's own 127.0.0.1 bind and Origin check — see the
    ///             per-key comment for what the gate does and does not cover.
    ///         </item>
    ///         <item>
    ///             <b>Never permitted</b> — everything else, in particular params that launch code
    ///             (<c>creator-hub-bin-path</c>, <c>launch-cdp-monitor-on-start</c> — SEC-005); point the client at
    ///             attacker infrastructure (<c>comms-adapter</c>, <c>gatekeeper-url</c>, <c>friends-api-url</c> —
    ///             SEC-052, <c>feature-flags-url</c>/<c>-hostname</c>, <c>optimized-assets-url</c>,
    ///             <c>lsd-remote-ab-server</c>/<c>-world</c>, <c>pulse</c>); bypass a version/specs screen
    ///             (<c>skip-version-check</c>, <c>skip-minimum-specs-screen</c>); or enable the remaining dev/test
    ///             modes (<c>debug</c>, <c>scene-console</c>, <c>autopilot</c>, <c>alttester</c>,
    ///             <c>simulate*</c>). A loopback realm does not unlock these: unlike the tier above, a key that is
    ///             in neither set is dropped for every realm.
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

            // Named spawn point within the destination scene (#9369). A landing refinement in the same class as
            // POSITION — it only picks where inside an already-permitted realm/position navigation the user arrives,
            // with no capability, infra, or exec impact.
            AppArgsFlags.SPAWN_POINT,
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

            // Starts the embedded MCP automation server so a coding agent can drive the client (#9339). The odd one
            // out in this set: the listener binds 127.0.0.1 with no auth token, so any local process can screenshot
            // the viewport, run chat commands as the signed-in user, and move the player. Loopback-gated because the
            // only launch that needs it is the local dev loop — sdk-commands forwards --mcp into a deep link whose
            // realm is the scene server it just started on 127.0.0.1. The gate drops the drive-by link that would
            // enable it against a production realm; it does not make the flag unreachable, since a crafted link can
            // supply a loopback realm of its own.
            AppArgsFlags.MCP,

            // Port the server above listens on. Presence alone also starts it (MCP_PORT implies MCP), so it carries
            // the same gate; the value is clamped to 1024-65535 and falls back to the default port (McpServerPlugin).
            AppArgsFlags.MCP_PORT,

            // Local-scene development only: load the scene's asset bundles from the preview server instead of raw
            // GLTFs. A pure boolean — the optimized-assets base is derived from the realm itself
            // ({realm}/optimized-assets, see RealmLaunchSettings.LocalAssetBundlesBaseUrl), the same value this
            // gate already requires to be loopback, so the flag adds no attacker-controllable input: it can only
            // point asset loading at the realm the link already targets. The full-URL variant
            // (optimized-assets-url) points AB/LOD/registry endpoints at arbitrary infrastructure and stays
            // never-permitted.
            AppArgsFlags.LOCAL_AB,
        };

        public static bool IsPermitted(string key) =>
            PERMITTED_KEYS.Contains(key);

        public static bool IsPermittedForLoopbackRealm(string key) =>
            LOOPBACK_REALM_PERMITTED_KEYS.Contains(key);
    }
}
