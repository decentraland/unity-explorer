
namespace ExplorerAutomationTests.Tests
{
    [TestFixture]
    [AllureSuite("Shortcuts Tests")]
    public class ShortcutsTests : BaseTest
    {
        [Test]
        public void TestOpenEventsWithShortcut()
        {
            PressKey(AltKeyCode.X);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True, "Events section should be visible after pressing X");
            Reporter.Log("Events section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenPlacesWithShortcut()
        {
            PressKey(AltKeyCode.Z);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Places.IsSectionVisible(), Is.True, "Places section should be visible after pressing Z");
            Reporter.Log("Places section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenCommunitiesWithShortcut()
        {
            PressKey(AltKeyCode.O);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Communities.IsSectionVisible(), Is.True, "Communities section should be visible after pressing O");
            Reporter.Log("Communities section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenMapWithShortcut()
        {
            PressKey(AltKeyCode.M);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Navmap.IsSectionVisible(), Is.True, "Navmap section should be visible after pressing M");
            Reporter.Log("Map section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenBackpackWithShortcut()
        {
            PressKey(AltKeyCode.I);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Backpack.IsSectionVisible(), Is.True, "Backpack section should be visible after pressing I");
            Reporter.Log("Backpack section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenGalleryWithShortcut()
        {
            PressKey(AltKeyCode.K);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Gallery.IsSectionVisible(), Is.True, "Gallery section should be visible after pressing K");
            Reporter.Log("Gallery section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenSettingsWithShortcut()
        {
            PressKey(AltKeyCode.P);
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Settings.IsSectionVisible(), Is.True, "Settings section should be visible after pressing P");
            Reporter.Log("Settings section opened via shortcut");

            PressEscape();
            ExplorePanelView.WaitForPanelClosed();
        }
    }
}
