using System;
using System.Collections.Generic;

namespace Global.AppArgs
{
    public static class DclDevGateway
    {
        private const string DOMAIN = "dcl.dev";
        public const string ORIGIN = "https://" + DOMAIN;
        public const string FEATURE_FLAGS_URL = ORIGIN + "/feature-flags";

        public static bool IsOrigin(string? gateway) =>
            Uri.TryCreate(gateway, UriKind.Absolute, out Uri? uri)
            && uri.AbsoluteUri.TrimEnd('/') == ORIGIN;

        public static bool UseAssetFlag(IAppArgs args, string flag)
        {
            if (args.TryGetValue(AppArgsFlags.GATEWAY, out string? gateway) && IsOrigin(gateway))
                return args.ResolveFeatureFlagArg(flag, fallback: false, requireDebug: false);

            return args.HasFlag(flag);
        }

        internal static void ApplyLaunchDefaults(Dictionary<string, string> args)
        {
            if (!args.TryGetValue(AppArgsFlags.GATEWAY, out string? gateway) || !IsOrigin(gateway))
                return;

            args.TryAdd(AppArgsFlags.BASE_DOMAIN, DOMAIN);
            args.TryAdd(AppArgsFlags.REALM, ORIGIN);
            args.TryAdd(AppArgsFlags.POSITION, "100,100");
            args.TryAdd(AppArgsFlags.ABGEN_PIPELINE, string.Empty);
            args.TryAdd(AppArgsFlags.ABGEN_LODS, string.Empty);
            args.TryAdd(AppArgsFlags.FeatureFlags.URL, FEATURE_FLAGS_URL);
            args.TryAdd(AppArgsFlags.GATEKEEPER_URL, ORIGIN + "/comms-gatekeeper");
            args.TryAdd(AppArgsFlags.PULSE_MULTIPLAYER, "true");
            args.TryAdd(AppArgsFlags.PULSE_SERVER, "dcl-one-poc-rt.decentraland.zone");
            args.TryAdd(AppArgsFlags.REALM_SCENE_POINTERS, string.Empty);
            args.TryAdd(AppArgsFlags.LANDSCAPE_TERRAIN_ENABLED, "false");
            args.TryAdd(AppArgsFlags.AUTH_BRIDGE_ONLY, string.Empty);
            args.TryAdd(AppArgsFlags.SKIP_VERSION_CHECK, string.Empty);
        }
    }
}
