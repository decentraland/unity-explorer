using System.Text.RegularExpressions;

namespace DCL.ApplicationGuards
{
    public static class SemanticVersioningExtensions
    {
        public static bool IsOlderThan(this string current, string latest) =>
            current.ToSemanticVersion().IsOlderThan(latest.ToSemanticVersion());

        private static bool IsOlderThan(this (int Major, int Minor, int Patch) current, (int Major, int Minor, int Patch) latest)
        {
            if (current.Major != latest.Major) return current.Major < latest.Major;
            if (current.Minor != latest.Minor) return current.Minor < latest.Minor;
            return current.Patch < latest.Patch;
        }

        private static (int Major, int Minor, int Patch) ToSemanticVersion(this string versionString)
        {
            Match match = Regex.Match(versionString, @"v?(\d+)\.?(\d*)\.?(\d*)");

            if (!match.Success) return (0, 0, 0); // Default if no version found

            var major = int.Parse(match.Groups[1].Value);
            int minor = ParseOptional(match.Groups[2]);
            int patch = ParseOptional(match.Groups[3]);
            return (major, minor, patch);
        }

        // The optional groups match an empty string when the component is absent
        private static int ParseOptional(Group group) =>
            group.Value.Length > 0 ? int.Parse(group.Value) : 0;
    }
}
