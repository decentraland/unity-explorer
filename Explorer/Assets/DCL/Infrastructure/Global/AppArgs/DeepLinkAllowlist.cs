using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace Global.AppArgs
{
    /// <summary>
    ///     Deny-by-default allowlist of query params a <c>decentraland://</c> deep link may inject into app-args.
    ///     Shared by the cold-start argv path and the runtime bridge path (both funnel through
    ///     <see cref="ApplicationParametersParser.ProcessDeepLinkParameters" />).
    ///     <para>
    ///     A deep link is fully attacker-controllable — anyone can craft one and get a victim to open it — so a
    ///     param is permitted only when it is a benign navigation / share / login intent whose worst case is
    ///     already gated elsewhere (a consent prompt, a matching login token, or a plain coordinate). Everything
    ///     else is dropped. In particular we never permit params that:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>launch code — <c>creator-hub-bin-path</c>, <c>launch-cdp-monitor-on-start</c> (SEC-005);</item>
    ///         <item>
    ///             point the client at attacker infrastructure — <c>comms-adapter</c>, <c>gatekeeper-url</c>,
    ///             <c>friends-api-url</c> (SEC-052), <c>feature-flags-url</c>/<c>-hostname</c>,
    ///             <c>optimized-assets-url</c>, <c>lsd-remote-ab-server</c>/<c>-world</c>, <c>pulse</c>;
    ///         </item>
    ///         <item>bypass a security/safety screen — <c>skip-auth-screen</c>, <c>skip-version-check</c>, <c>skip-minimum-specs-screen</c>;</item>
    ///         <item>switch environment — <c>dclenv</c>;</item>
    ///         <item>
    ///             enable a dev/debug/test mode — <c>debug</c>, <c>hub</c>, <c>scene-console</c>, <c>autopilot</c>,
    ///             <c>alttester</c>, <c>simulate*</c>, and <c>local-scene</c> (SEC-020: dropped here and re-permitted
    ///             only for a loopback realm in <see cref="ApplicationParametersParser.ProcessDeepLinkParameters" />);
    ///         </item>
    ///         <item>tamper with feature gates, cache/disk, identity/session, chat limits, rendering, or analytics ids.</item>
    ///     </list>
    ///     The permitted set is a product decision (SEC-019/020 "Design affected") — changing it requires sign-off.
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

        public static bool IsPermitted(string key) =>
            PERMITTED_KEYS.Contains(key);
    }
}
