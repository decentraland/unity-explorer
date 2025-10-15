using DCL.Utilities;
using NUnit.Framework;

namespace DCL.Translation
{
    [TestFixture]
    public class LanguageCodeParserShould
    {
        [TestCase("zh-Hans", LanguageCode.ZH)]
        [TestCase("ZH-Hant", LanguageCode.ZH)]
        [TestCase("zh-CN",   LanguageCode.ZH)]
        [TestCase("zh_TW",   LanguageCode.ZH)]
        [TestCase("pt-BR",   LanguageCode.PT)]
        [TestCase("PT-pt",   LanguageCode.PT)]
        [TestCase("es-419",  LanguageCode.ES)]
        [TestCase("en-US",   LanguageCode.EN)]
        [TestCase("EN",      LanguageCode.EN)]
        [TestCase("ru",      LanguageCode.RU)]
        [TestCase("Ko",      LanguageCode.KO)]
        public void MapCommonBcp47VariantsToSupportedLanguage(string input, LanguageCode expected)
        {
            var result = LanguageCodeParser.Parse(input);
            Assert.AreEqual(expected, result);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("xx-YY")]
        [TestCase("cmn-Hans-CN")] // Mandarin variant
        public void FallbackToEnglishOnUnknownOrEmpty(string input)
        {
            var result = LanguageCodeParser.Parse(input);
            Assert.AreEqual(LanguageCode.EN, result);
        }

        [Test]
        public void UsePrimarySubtagWhenNoDirectMapExists()
        {
            Assert.AreEqual(LanguageCode.FR, LanguageCodeParser.Parse("fr-CA"));
            Assert.AreEqual(LanguageCode.DE, LanguageCodeParser.Parse("de-Latn-DE"));
            Assert.AreEqual(LanguageCode.IT, LanguageCodeParser.Parse("it_IT"));
        }

        [Test]
        public void BeCaseAndSeparatorInsensitive()
        {
            Assert.AreEqual(LanguageCode.ZH, LanguageCodeParser.Parse("Zh_hAnS"));
            Assert.AreEqual(LanguageCode.EN, LanguageCodeParser.Parse("en-us"));
            Assert.AreEqual(LanguageCode.PT, LanguageCodeParser.Parse("PT_br"));
        }

        [Test]
        public void IgnoreExtensionsAndPrivateUseSubtags()
        {
            Assert.AreEqual(LanguageCode.EN, LanguageCodeParser.Parse("en-US-x-custom"));
            Assert.AreEqual(LanguageCode.ZH, LanguageCodeParser.Parse("zh-Hans-CN-u-ca-chinese"));
        }
    }
}