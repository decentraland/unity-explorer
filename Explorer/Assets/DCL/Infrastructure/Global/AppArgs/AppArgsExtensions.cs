using UnityEngine;

namespace Global.AppArgs
{
    public static class AppArgsExtensions
    {
        public static bool HasDebugFlag(this IAppArgs args, bool checkDebugBuild = true) =>
            (checkDebugBuild && Debug.isDebugBuild) || args.HasFlag(AppArgsFlags.DEBUG);

        public static bool HasFlagWithValueTrue(this IAppArgs args, string flagName) =>
            args.TryGetValue(flagName, out var flagValue) && flagValue == "true";

        public static bool HasFlagWithValueFalse(this IAppArgs args, string flagName) =>
            args.TryGetValue(flagName, out var flagValue) && flagValue == "false";

        //This method resolves a feature flag considering an eventual app argument override, allowing to set
        //the value to both true or false
        public static bool ResolveFeatureFlagArg(this IAppArgs args, string appArgFlag, bool fallback, bool requireDebug = true)
        {
            if (!args.HasFlag(appArgFlag))
                return fallback;

            if (args.HasFlagWithValueFalse(appArgFlag))
                return false;

            if (requireDebug && !args.HasDebugFlag())
                return fallback;

            return true;
        }
    }
}
