using DCL.MarketplaceCredits.Purchase.Names;
using NUnit.Framework;

namespace DCL.MarketplaceCredits.Purchase.Tests
{
    public class NameRulesShould
    {
        [TestCase("", ExpectedResult = NameValidation.Empty)]
        [TestCase("a", ExpectedResult = NameValidation.TooShort)]
        [TestCase("ab", ExpectedResult = NameValidation.Valid)]
        [TestCase("Carolo", ExpectedResult = NameValidation.Valid)]
        [TestCase("Name123", ExpectedResult = NameValidation.Valid)]
        [TestCase("abcdefghijklmno", ExpectedResult = NameValidation.Valid)]
        [TestCase("abcdefghijklmnop", ExpectedResult = NameValidation.TooLong)]
        [TestCase("with space", ExpectedResult = NameValidation.InvalidCharacters)]
        [TestCase("dash-ed", ExpectedResult = NameValidation.InvalidCharacters)]
        [TestCase("ñandu", ExpectedResult = NameValidation.InvalidCharacters)]
        [TestCase("name.dcl.eth", ExpectedResult = NameValidation.InvalidCharacters)]
        public NameValidation ValidateTheNameFormat(string name) =>
            NameRules.Validate(name);

        [TestCase('a', ExpectedResult = true)]
        [TestCase('Z', ExpectedResult = true)]
        [TestCase('7', ExpectedResult = true)]
        [TestCase('é', ExpectedResult = false)]
        [TestCase('_', ExpectedResult = false)]
        [TestCase(' ', ExpectedResult = false)]
        public bool AllowOnlyAsciiLettersAndDigits(char c) =>
            NameRules.IsAllowedChar(c);
    }
}
