using System;
using System.Collections.Generic;

namespace Global.AppArgs
{
    public static class DclDevGateway
    {
        private const string DOMAIN = "dcl.dev";
        public const string ORIGIN = "https://" + DOMAIN;
        public const string FEATURE_FLAGS_URL = ORIGIN + "/feature-flags";
        public const string SOCIAL_RPC_URL = "wss://" + DOMAIN + "/social-rpc";

        private const string GATEKEEPER_URL = ORIGIN + "/comms-gatekeeper";

        /// <summary>The Pulse relay dcl.dev pairs with; owned by the deployment, not by a Decentraland environment.</summary>
        private const string PULSE_HOST = "dcl-one-poc-rt.decentraland.zone";

        /// <summary>The parcel a dcl.dev launch starts on when no --position is given.</summary>
        private const string START_POSITION = "100,100";

        public static bool IsOrigin(string? gateway) =>
            Uri.TryCreate(gateway, UriKind.Absolute, out Uri? uri)
            && uri.AbsoluteUri.TrimEnd('/') == ORIGIN;

        /// <summary>
        ///     Whether <paramref name="flag" /> forces its asset source on. Under dcl.dev the flag is a default the
        ///     launch can turn off with the value "false"; elsewhere its presence alone decides.
        /// </summary>
        /// <param name="cliGatewayPrefix">The gateway the command line named, as captured before any deep link was merged</param>
        public static bool UseAssetFlag(IAppArgs args, string flag, string? cliGatewayPrefix) =>
            IsOrigin(cliGatewayPrefix)
                ? args.ResolveFeatureFlagArg(flag, fallback: false, requireDebug: false)
                : args.HasFlag(flag);

        internal static void ApplyLaunchDefaults(Dictionary<string, string> args)
        {
            if (!args.TryGetValue(AppArgsFlags.GATEWAY, out string? gateway) || !IsOrigin(gateway))
                return;

            args.TryAdd(AppArgsFlags.BASE_DOMAIN, DOMAIN);
            args.TryAdd(AppArgsFlags.REALM, ORIGIN);
            args.TryAdd(AppArgsFlags.POSITION, START_POSITION);
            args.TryAdd(AppArgsFlags.ABGEN_PIPELINE, string.Empty);
            args.TryAdd(AppArgsFlags.ABGEN_LODS, string.Empty);
            args.TryAdd(AppArgsFlags.FeatureFlags.URL, FEATURE_FLAGS_URL);
            args.TryAdd(AppArgsFlags.GATEKEEPER_URL, GATEKEEPER_URL);
            args.TryAdd(AppArgsFlags.PULSE_MULTIPLAYER, "true");
            args.TryAdd(AppArgsFlags.PULSE_SERVER, PULSE_HOST);
            args.TryAdd(AppArgsFlags.REALM_SCENE_POINTERS, string.Empty);
            args.TryAdd(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED, "false");
            args.TryAdd(AppArgsFlags.AUTH_BRIDGE_ONLY, string.Empty);
            args.TryAdd(AppArgsFlags.SKIP_VERSION_CHECK, string.Empty);
        }
    }
}
