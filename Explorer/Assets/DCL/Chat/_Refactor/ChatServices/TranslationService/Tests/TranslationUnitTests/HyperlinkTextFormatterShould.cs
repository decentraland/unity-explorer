using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using DCL.Profiles;
using DCL.UI.InputFieldFormatting;

namespace DCL.Translation.Tests
{
    public static class HyperlinkConstants
    {
        public const string URL = "url";
        public const string SCENE = "scene";
        public const string WORLD = "world";
        public const string PROFILE = "profile";
    }

    public static class GenesisCityData
    {
        private const int MIN_COORD = -150;
        private const int MAX_COORD = 150;

        public static bool IsInsideBounds(int x, int y)
        {
            return x >= MIN_COORD && x <= MAX_COORD && y >= MIN_COORD && y <= MAX_COORD;
        }
    }


    [TestFixture]
    public class HyperlinkTextFormatterShould
    {
        private HyperlinkTextFormatter formatter;
        private IProfileCache profileCache;

        private const string LINK_OPENING_STYLE = "<#00B2FF><link=";
        private const string LINK_CLOSING_STYLE = "</link></color>";
        private const string OWN_PROFILE_OPENING_STYLE = "<#00B2FF>";
        private const string OWN_PROFILE_CLOSING_STYLE = "</color>";

        [SetUp]
        public void SetUp()
        {
            // We only need to substitute the profile cache.
            profileCache = Substitute.For<IProfileCache>();

            // The key change: We must pass null for the SelfProfile instance because we cannot
            // construct it without its many dependencies. This is safe as long as we do not
            // test features that try to use this null object (i.e., username formatting).
            formatter = new HyperlinkTextFormatter(profileCache, null);
        }

        [Test]
        [TestCase("hello world", "hello world", TestName = "Plain text remains unchanged")]
        [TestCase("", "", TestName = "Empty input returns empty output")]
        [TestCase("/help", "/help", TestName = "Commands starting with slash are ignored")]
        [TestCase("  leading space", "  leading space", TestName = "Leading spaces are preserved")]
        public void FormatTextWithGeneralInput(string input, string expected)
        {
            string result = formatter.FormatText(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase("Check https://decentraland.org", "Check <#00B2FF><link=url>https://decentraland.org</link></color>", TestName = "URL at the end")]
        [TestCase("http://dcl.gg is a cool site", "<#00B2FF><link=url>http://dcl.gg</link></color> is a cool site", TestName = "URL at the beginning")]
        [TestCase("Visit https://a.co and see", "Visit <#00B2FF><link=url>https://a.co</link></color> and see", TestName = "Short URL in the middle")]
        [TestCase("No link here www.google.com", "No link here www.google.com", TestName = "URL without http or https is ignored")]
        [TestCase("Link with path: http://test.com/path/1", "Link with path: <#00B2FF><link=url>http://test.com/path/1</link></color>", TestName = "URL with a path")]
        public void FormatTextWithUrls(string input, string expected)
        {
            string result = formatter.FormatText(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase("Go to 10,20", "Go to <#00B2FF><link=scene>10,20</link></color>", TestName = "Valid coordinates")]
        [TestCase("Meet me at -150,-150.", "Meet me at <#00B2FF><link=scene>-150,-150</link></color>.", TestName = "Valid negative boundary coordinates with punctuation")]
        [TestCase("This is not a coord 1, 2", "This is not a coord 1, 2", TestName = "Coordinates with space are ignored")]
        [TestCase("Coords can be -10,100!", "Coords can be <#00B2FF><link=scene>-10,100</link></color>!", TestName = "Coordinates followed by exclamation mark")]
        public void FormatTextWithSceneCoords(string input, string expected)
        {
            string result = formatter.FormatText(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        [TestCase("Visit myworld.dcl.eth now", "Visit <#00B2FF><link=world>myworld.dcl.eth</link></color> now", TestName = "World name in the middle")]
        [TestCase("cool.dcl.eth is my world", "<#00B2FF><link=world>cool.dcl.eth</link></color> is my world", TestName = "World name at the beginning")]
        [TestCase("This is not a world: test.dcl.eth.", "This is not a world: <#00B2FF><link=world>test.dcl.eth</link></color>.", TestName = "World name followed by a period")]
        [TestCase("Invalid world name myworld.eth", "Invalid world name myworld.eth", TestName = "Invalid TLD is ignored")]
        public void FormatTextWithWorldNames(string input, string expected)
        {
            string result = formatter.FormatText(input);
            Assert.AreEqual(expected, result);
        }

        // [Test]
        // [TestCase("Hello @ValidUser, how are you?", "Hello <#00B2FF><link=PROFILE>@ValidUser</link></color>, how are you?", TestName = "Existing user is formatted")]
        // [TestCase("This is me, @OwnUser", "This is me, <#00B2FF>@OwnUser</color>", TestName = "Own username is formatted differently")]
        // [TestCase("Who is @UnknownUser?", "Who is @UnknownUser?", TestName = "Non-existing user is ignored")]
        // [TestCase("A message for @ValidUser#1234", "A message for <#00B2FF><link=PROFILE>@ValidUser#1234</link></color>", TestName = "Username with discriminator is formatted")]
        // [TestCase("Username @ValidUser.", "Username <#00B2FF><link=PROFILE>@ValidUser</link></color>.", TestName = "Username followed by punctuation")]
        // [TestCase("Invalid format user@name", "Invalid format user@name", TestName = "Email-like format is ignored")]
        // public void FormatTextWithUsernames(string input, string expected)
        // {
        //     string result = formatter.FormatText(input);
        //     Assert.AreEqual(expected, result);
        // }

        [Test]
        [TestCase("This should be escaped: <br>", "This should be escaped: ‹br›", TestName = "Simple tag is escaped")]
        [TestCase("This should not: <b>bold</b>", "This should not: <b>bold</b>", TestName = "<b> tag is not escaped")]
        [TestCase("This should not: <i>italic</i>", "This should not: <i>italic</i>", TestName = "<i> tag is not escaped")]
        [TestCase("Multiple <br> and <hr>", "Multiple ‹br› and ‹hr›", TestName = "Multiple tags are escaped")]
        [TestCase("Tag with attributes <font color='red'>", "Tag with attributes ‹font color='red'›", TestName = "Tag with attributes is escaped")]
        public void FormatTextWithRichText(string input, string expected)
        {
            string result = formatter.FormatText(input);
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void FormatTextWithMixedContent()
        {
            // Arrange
            string input = "Hi, let's meet at 50,-50 Check out my world at awesome.dcl.eth and this site https://dcl.gg.com";
            string expected = $"Hi, let's meet at {LINK_OPENING_STYLE}{HyperlinkConstants.SCENE}>50,-50{LINK_CLOSING_STYLE} Check out my world at {LINK_OPENING_STYLE}{HyperlinkConstants.WORLD}>awesome.dcl.eth{LINK_CLOSING_STYLE} and this site {LINK_OPENING_STYLE}{HyperlinkConstants.URL}>https://dcl.gg.com{LINK_CLOSING_STYLE}";

            // Act
            string result = formatter.FormatText(input);

            // Assert
            Assert.AreEqual(expected, result);
        }

        [Test]
        public void GetMatchesWithVariousPatterns()
        {
            // Arrange
            string input = "Hi, go to 10,20 or https://decentraland.org and visit myworld.dcl.eth";
            var matchesResult = new List<(TextFormatMatchType, Match)>();

            // Act
            formatter.GetMatches(input, matchesResult);

            // Assert
            Assert.AreEqual(3, matchesResult.Count);

            Assert.AreEqual(TextFormatMatchType.SCENE, matchesResult[0].Item1);
            Assert.AreEqual("10,20", matchesResult[0].Item2.Value);

            Assert.AreEqual(TextFormatMatchType.URL, matchesResult[1].Item1);
            Assert.AreEqual("https://decentraland.org", matchesResult[1].Item2.Value);

            Assert.AreEqual(TextFormatMatchType.WORLD, matchesResult[2].Item1);
            Assert.AreEqual("myworld.dcl.eth", matchesResult[2].Item2.Value);
        }
    }
}