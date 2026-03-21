using ExplorerAutomationTests.Views.Sections;

namespace ExplorerAutomationTests.Views
{
    public class ExplorePanelView : BaseView
    {
        // Panel
        private readonly (By, string) _panelLocator = (By.ID, "d5383a2a-d281-4fe8-b53b-fee873f32654");
        private readonly (By, string) _closeButtonLocator = (By.ID, "f507113e-bb78-4ddb-9d3e-4338e1f75dfe");

        // Tab buttons
        private readonly (By, string) _eventsTabLocator = (By.ID, "8b6ee3fb-097b-46b5-9d6a-e6ca21f737f0");
        private readonly (By, string) _placesTabLocator = (By.ID, "261fa576-8df6-496e-82f0-dd11c2592086");
        private readonly (By, string) _communitiesTabLocator = (By.ID, "d696490d-ba13-4701-ad08-e617c2dbdd74");
        private readonly (By, string) _mapTabLocator = (By.ID, "48d169c6-427d-4bb3-8bde-a2f06851b387");
        private readonly (By, string) _backpackTabLocator = (By.ID, "a5f6205e-84a2-4a68-9638-e1d27baf37e0");
        private readonly (By, string) _galleryTabLocator = (By.ID, "80fd3d49-bd26-4700-91ce-c50f97bce0b4");
        private readonly (By, string) _settingsTabLocator = (By.ID, "0107ddd9-a087-4fa5-885d-b47df8854ff9");

        // Sections
        public EventsSection Events { get; }
        public PlacesSection Places { get; }
        public CommunitiesSection Communities { get; }
        public NavmapSection Navmap { get; }
        public BackpackSection Backpack { get; }
        public GallerySection Gallery { get; }
        public SettingsSection Settings { get; }

        public ExplorePanelView(AltDriver altDriver) : base(altDriver)
        {
            Events = new EventsSection(altDriver);
            Places = new PlacesSection(altDriver);
            Communities = new CommunitiesSection(altDriver);
            Navmap = new NavmapSection(altDriver);
            Backpack = new BackpackSection(altDriver);
            Gallery = new GallerySection(altDriver);
            Settings = new SettingsSection(altDriver);
        }

        [AllureStep("Wait for explore panel to open")]
        public void WaitForPanelOpen(int timeoutSeconds = 10)
        {
            WaitForObject(_panelLocator, timeoutSeconds);
            Reporter.Log("Explore panel is open");
        }

        [AllureStep("Check if explore panel is visible")]
        public bool IsPanelVisible()
        {
            return IsObjectPresent(_panelLocator);
        }

        [AllureStep("Wait for explore panel to close")]
        public void WaitForPanelClosed(int timeoutSeconds = 10)
        {
            WaitForObjectNotBePresent(_panelLocator, timeoutSeconds);
            Reporter.Log("Explore panel is closed");
        }

        [AllureStep("Click close button")]
        public void ClickClose()
        {
            ClickObject(_closeButtonLocator);
            Reporter.Log("Clicked explore panel close button");
        }

        [AllureStep("Click Events tab")]
        public void ClickEventsTab()
        {
            ClickObject(_eventsTabLocator);
            Reporter.Log("Clicked Events tab");
        }

        [AllureStep("Click Places tab")]
        public void ClickPlacesTab()
        {
            ClickObject(_placesTabLocator);
            Reporter.Log("Clicked Places tab");
        }

        [AllureStep("Click Communities tab")]
        public void ClickCommunitiesTab()
        {
            ClickObject(_communitiesTabLocator);
            Reporter.Log("Clicked Communities tab");
        }

        [AllureStep("Click Map tab")]
        public void ClickMapTab()
        {
            ClickObject(_mapTabLocator);
            Reporter.Log("Clicked Map tab");
        }

        [AllureStep("Click Backpack tab")]
        public void ClickBackpackTab()
        {
            ClickObject(_backpackTabLocator);
            Reporter.Log("Clicked Backpack tab");
        }

        [AllureStep("Click Gallery tab")]
        public void ClickGalleryTab()
        {
            ClickObject(_galleryTabLocator);
            Reporter.Log("Clicked Gallery tab");
        }

        [AllureStep("Click Settings tab")]
        public void ClickSettingsTab()
        {
            ClickObject(_settingsTabLocator);
            Reporter.Log("Clicked Settings tab");
        }
    }
}
