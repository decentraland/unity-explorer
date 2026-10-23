using System.Text.RegularExpressions;
using Utility;

namespace DCL.UI.InputFieldFormatting
{
    /// <summary>
    ///     What counts as a Genesis City parcel or a world name in typed text, shared by the chat's hyperlinks and the
    ///     lobby's go-to field.
    /// </summary>
    public static class DestinationRules
    {
        public const string WORLD_SUFFIX = ".dcl.eth";

        internal const string WORLD_NAME_PATTERN = "[A-Za-z0-9]+";
        internal const string WORLD_PATTERN = WORLD_NAME_PATTERN + @"\.dcl\.eth";

        private static readonly Regex WORLD_REGEX = new ($"^{WORLD_PATTERN}$", RegexOptions.Compiled);
        private static readonly Regex WORLD_SHORT_NAME_REGEX = new ($"^{WORLD_NAME_PATTERN}$", RegexOptions.Compiled);

        public static bool IsParcel(int x, int y) =>
            GenesisCityData.IsInsideBounds(x, y);

        public static bool IsWorld(string name) =>
            WORLD_REGEX.IsMatch(name);

        public static bool IsWorldShortName(string name) =>
            WORLD_SHORT_NAME_REGEX.IsMatch(name);
    }
}
