using System.Text.RegularExpressions;

namespace DCL.ApplicationVersionGuard
{
    public static class SemanticVersioningExtensions
    {
        public static bool IsOlderThan(this string current, string latest) =>
            current.ToSemanticVersion().IsOlderThan(latest.ToSemanticVersion());

        private static bool IsOlderThan(this (int Major, int Minor, int Patch) current, (int Major, int Minor, int Patch) latest)
        {
            if (current.Major < latest.Major) return true;
            if (current.Minor < latest.Minor) return true;
            return current.Patch < latest.Patch;
        }

        private static (int Major, int Minor, int Patch) ToSemanticVersion(this string versionString)
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
