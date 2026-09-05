using System.Text.RegularExpressions;

namespace DCL.Passport.Modals
{
    public static class LinkValidator
    {
        private static readonly Regex HTTP_REGEX = new (@"^(?:https?):\/\/[^\s\/$.?#].[^\s]*$");
        public static bool IsValid(string url) => HTTP_REGEX.IsMatch(url);
    }
}
