#if MCP_TEST_AUTOMATION
using DCL.McpServer.Utils;
using NUnit.Framework;

namespace DCL.McpServer.Tests
{
    public class UiElementPathShould
    {
        private const string PATH = "ugui:/Canvas/Menu/PlayButton";
        private const string NAME = "PlayButton";

        [Test]
        public void RecognizeTheSystemPrefix()
        {
            Assert.That(UiElementPath.IsUgui("ugui:/Canvas/Button"), Is.True);
            Assert.That(UiElementPath.IsUgui("uitk:/Doc/button"), Is.False);
            Assert.That(UiElementPath.IsUitk("uitk:/Doc/button"), Is.True);
            Assert.That(UiElementPath.IsUitk("ugui:/Canvas/Button"), Is.False);
        }

        [Test]
        public void IndexASegmentOnlyWhenItsNameIsSharedOrMissing()
        {
            Assert.That(UiElementPath.Segment("Play", "Button", 3, false), Is.EqualTo("Play"));
            Assert.That(UiElementPath.Segment("Item(Clone)", "GameObject", 2, true), Is.EqualTo("Item(Clone)[2]"));
            Assert.That(UiElementPath.Segment(null, "VisualElement", 3, false), Is.EqualTo("VisualElement[3]"));
            Assert.That(UiElementPath.Segment("", "Label", 0, false), Is.EqualTo("Label[0]"));
        }

        [Test]
        public void JoinSegmentsWithASingleSeparator()
        {
            Assert.That(UiElementPath.Join("uitk:/Doc", "Panel"), Is.EqualTo("uitk:/Doc/Panel"));
        }

        [TestCase(null, ExpectedResult = true)]
        [TestCase("", ExpectedResult = true)]
        [TestCase("play", ExpectedResult = true)]   // matches name (case-insensitive)
        [TestCase("Canvas", ExpectedResult = true)] // matches path
        [TestCase("missing", ExpectedResult = false)]
        public bool MatchOnEitherNameOrPathCaseInsensitively(string? filter) =>
            UiElementPath.Matches("PlayButton", "ugui:/Canvas/PlayButton", filter);

        [TestCase(PATH, NAME, PATH, ExpectedResult = UiElementPath.SCORE_EXACT_PATH)]
        [TestCase(PATH, NAME, "//Menu/PlayButton", ExpectedResult = UiElementPath.SCORE_PATH_QUERY)]
        [TestCase(PATH, NAME, "playbutton", ExpectedResult = UiElementPath.SCORE_EXACT_NAME)] // exact name, case-insensitive
        [TestCase(PATH, "Other", "PlayButton", ExpectedResult = UiElementPath.SCORE_PATH_SUFFIX)]
        [TestCase(PATH, "Other", "Menu", ExpectedResult = UiElementPath.SCORE_PATH_CONTAINS)]
        [TestCase(PATH, "Other", "Canvas", ExpectedResult = UiElementPath.SCORE_PATH_CONTAINS)] // no separator: reads as a name lookup, so a sibling's name cannot win a path match
        [TestCase(PATH, NAME, "nope", ExpectedResult = 0)]
        [TestCase(PATH, NAME, "", ExpectedResult = 0)]
        public int ScoreAnExactPathAboveAPathQueryAboveANameAboveASuffixAboveAContains(string path, string name, string query) =>
            UiElementPath.MatchScore(path, name, query);

        [TestCase("//Canvas/Menu/PlayButton", ExpectedResult = true)]  // descendant root, then two children
        [TestCase("//Canvas//PlayButton", ExpectedResult = true)]      // skips the intermediate node
        [TestCase("//PlayButton", ExpectedResult = true)]              // anywhere under the root
        [TestCase("/Canvas/Menu/PlayButton", ExpectedResult = true)]   // anchored at the hierarchy root
        [TestCase("Menu/PlayButton", ExpectedResult = true)]           // no leading separator reads as descendant
        [TestCase("//Menu/*", ExpectedResult = true)]                  // wildcard segment
        [TestCase("//menu//playbutton", ExpectedResult = true)]        // case-insensitive
        [TestCase("ugui://Menu/PlayButton", ExpectedResult = true)]    // system-prefixed query
        [TestCase("uitk://Menu/PlayButton", ExpectedResult = false)]   // wrong UI system
        [TestCase("//Canvas/PlayButton", ExpectedResult = false)]      // Menu is not skippable behind a single separator
        [TestCase("//Canvas/Menu", ExpectedResult = false)]            // must identify the leaf, not an ancestor
        [TestCase("/Menu/PlayButton", ExpectedResult = false)]         // anchored, but Menu is not the root
        [TestCase("//Canvas//Missing", ExpectedResult = false)]
        public bool MatchPathExpressions(string query) =>
            UiElementPath.MatchesQuery(PATH, query);

        [TestCase("ugui:/Canvas/Grid/Item(Clone)[2]", "//Grid/Item(Clone)[2]", ExpectedResult = true)]
        [TestCase("ugui:/Canvas/Grid/Item(Clone)[2]", "//Grid/Item(Clone)", ExpectedResult = true)]   // an index-less query accepts any index
        [TestCase("ugui:/Canvas/Grid/Item(Clone)[2]", "//Grid/Item(Clone)[1]", ExpectedResult = false)]
        [TestCase("ugui:/Canvas/Grid/Item", "//Grid/Item[0]", ExpectedResult = true)]                  // an unindexed candidate still matches an explicit [0]
        [TestCase("ugui:/Canvas/Grid/Item", "//Grid/Item[1]", ExpectedResult = false)]                 // ...but not any other explicit index
        public bool MatchTheSiblingIndexer(string candidatePath, string query) =>
            UiElementPath.MatchesQuery(candidatePath, query);

        [Test]
        public void KeepNodeNamesWithSpacesDotsAndParenthesesIntact()
        {
            const string CLONED = "ugui:/Root/Lobby.NewAccount.Screen/AvatarButtons.Container/Text (TMP)";

            Assert.That(UiElementPath.MatchesQuery(CLONED, "//Lobby.NewAccount.Screen//AvatarButtons.Container/Text (TMP)"), Is.True);
            Assert.That(UiElementPath.MatchesQuery(CLONED, "//AvatarButtons.Container/Text"), Is.False);
        }

        [Test]
        public void RejectAnEmptyOrSeparatorOnlyQuery()
        {
            Assert.That(UiElementPath.MatchesQuery(PATH, string.Empty), Is.False);
            Assert.That(UiElementPath.MatchesQuery(PATH, "//"), Is.False);
            Assert.That(UiElementPath.MatchesQuery(string.Empty, "//PlayButton"), Is.False);
        }
    }
}
#endif
