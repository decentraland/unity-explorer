
namespace ExplorerAutomationTests.Tests
{
    [TestFixture]
    [AllureSuite("Explore Panel Tests")]
    public class ExplorePanelTests : InWorldBaseTest
    {
        [Test]
        public void TestOpenEventsFromSidebar()
        {
            MainMenuView.ClickEvents();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True, "Events section should be visible");
            Reporter.Log("Events section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenPlacesFromSidebar()
        {
            MainMenuView.ClickPlaces();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Places.IsSectionVisible(), Is.True, "Places section should be visible");
            Reporter.Log("Places section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenCommunitiesFromSidebar()
        {
            MainMenuView.ClickCommunities();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Communities.IsSectionVisible(), Is.True, "Communities section should be visible");
            Reporter.Log("Communities section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenMapFromSidebar()
        {
            MainMenuView.ClickMap();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Navmap.IsSectionVisible(), Is.True, "Navmap section should be visible");
            Reporter.Log("Map section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenBackpackFromSidebar()
        {
            MainMenuView.ClickBackpack();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Backpack.IsSectionVisible(), Is.True, "Backpack section should be visible");
            Reporter.Log("Backpack section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenGalleryFromSidebar()
        {
            MainMenuView.ClickGallery();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Gallery.IsSectionVisible(), Is.True, "Gallery section should be visible");
            Reporter.Log("Gallery section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestOpenSettingsFromSidebar()
        {
            MainMenuView.ClickSettings();
            ExplorePanelView.WaitForPanelOpen();

            Assert.That(ExplorePanelView.Settings.IsSectionVisible(), Is.True, "Settings section should be visible");
            Reporter.Log("Settings section opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }

        [Test]
        public void TestSwitchBetweenAllTabs()
        {
            // Open the panel via any sidebar button
            MainMenuView.ClickEvents();
            ExplorePanelView.WaitForPanelOpen();

            // Events tab
            ExplorePanelView.ClickEventsTab();
            Assert.That(ExplorePanelView.Events.IsSectionVisible(), Is.True, "Events section should be visible after clicking Events tab");
            Reporter.Log("Events tab opened successfully", withScreenshot: true);

            // Places tab
            ExplorePanelView.ClickPlacesTab();
            Assert.That(ExplorePanelView.Places.IsSectionVisible(), Is.True, "Places section should be visible after clicking Places tab");
            Reporter.Log("Places tab opened successfully", withScreenshot: true);

            // Communities tab
            ExplorePanelView.ClickCommunitiesTab();
            Assert.That(ExplorePanelView.Communities.IsSectionVisible(), Is.True, "Communities section should be visible after clicking Communities tab");
            Reporter.Log("Communities tab opened successfully", withScreenshot: true);

            // Map tab
            ExplorePanelView.ClickMapTab();
            Assert.That(ExplorePanelView.Navmap.IsSectionVisible(), Is.True, "Navmap section should be visible after clicking Map tab");
            Reporter.Log("Map tab opened successfully", withScreenshot: true);

            // Backpack tab
            ExplorePanelView.ClickBackpackTab();
            Assert.That(ExplorePanelView.Backpack.IsSectionVisible(), Is.True, "Backpack section should be visible after clicking Backpack tab");
            Reporter.Log("Backpack tab opened successfully", withScreenshot: true);

            // Gallery tab
            ExplorePanelView.ClickGalleryTab();
            Assert.That(ExplorePanelView.Gallery.IsSectionVisible(), Is.True, "Gallery section should be visible after clicking Gallery tab");
            Reporter.Log("Gallery tab opened successfully", withScreenshot: true);

            // Settings tab
            ExplorePanelView.ClickSettingsTab();
            Assert.That(ExplorePanelView.Settings.IsSectionVisible(), Is.True, "Settings section should be visible after clicking Settings tab");
            Reporter.Log("Settings tab opened successfully", withScreenshot: true);

            ExplorePanelView.ClickClose();
            ExplorePanelView.WaitForPanelClosed();
        }
    }
}
