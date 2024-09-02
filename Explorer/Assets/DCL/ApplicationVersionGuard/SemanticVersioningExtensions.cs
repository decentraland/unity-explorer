using System.Text.RegularExpressions;

namespace DCL.ApplicationVersionGuard
{
    public static class SemanticVersioningExtensions
    {
        public static bool IsOlderThan(this string current, string latest) =>
            current.ToSemanticVersion().IsOlderThan(latest.ToSemanticVersion());

        private static bool IsOlderThan(this (int, int, int) current, (int, int, int) latest)
        {
            if (current.Item1 < latest.Item1) return true;
            if (current.Item2 < latest.Item2) return true;
            return current.Item3 < latest.Item3;
        }

        private static (int, int, int) ToSemanticVersion(this string versionString)
        {
            Match match = Regex.Match(versionString, @"v?(\d+)\.?(\d*)\.?(\d*)");

            if (!match.Success) return (0, 0, 0); // Default if no version found

            var major = int.Parse(match.Groups[1].Value);
            int minor = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
            int patch = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
            return (major, minor, patch);
        }
    }
}
