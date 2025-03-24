using System.Collections.Generic;
using UnityEngine;

namespace Global.AppArgs
{
    public interface IAppArgs
    {
        bool HasFlag(string flagName);

        bool TryGetValue(string flagName, out string? value);

        IEnumerable<string> Flags();
    }

    public static class AppArgsExtensions
    {
        public static bool HasDebugFlag(this IAppArgs args, bool checkDebugBuild = true) =>
            (checkDebugBuild && Debug.isDebugBuild) || args.HasFlag(AppArgsFlags.DEBUG);

        public static bool HasFlagWithValueTrue(this IAppArgs args, string flagName) =>
            args.TryGetValue(flagName, out var flagValue) && flagValue == "true";

        public static bool HasFlagWithValueFalse(this IAppArgs args, string flagName) =>
            args.TryGetValue(flagName, out var flagValue) && flagValue == "false";
    }
}
