namespace DCL.MarketplaceCredits.Purchase.Names
{
    public enum NameValidation
    {
        Valid,
        Empty,
        InvalidCharacters,
        TooShort,
        TooLong,
    }

    /// <summary>
    ///     The format a NAME must have to be registered. Port of the shop web app's lib/names.ts validateName;
    ///     the credits-server enforces the same rule (^[a-zA-Z0-9]{2,15}$) before it signs a route.
    /// </summary>
    public static class NameRules
    {
        public const int MIN_LENGTH = 2;
        public const int MAX_LENGTH = 15;

        // ASCII only: char.IsLetterOrDigit accepts letters the registrar rejects.
        public static bool IsAllowedChar(char c) =>
            c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';

        public static NameValidation Validate(string name)
        {
            if (name.Length == 0)
                return NameValidation.Empty;

            foreach (char c in name)
                if (!IsAllowedChar(c))
                    return NameValidation.InvalidCharacters;

            if (name.Length < MIN_LENGTH)
                return NameValidation.TooShort;

            return name.Length > MAX_LENGTH ? NameValidation.TooLong : NameValidation.Valid;
        }
    }
}
