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
        public static bool HasDebugFlag(this IAppArgs args) =>
            Debug.isDebugBuild || args.HasFlag(AppArgsFlags.DEBUG);
    }
}
