
namespace ExplorerAutomationTests.Views
{
    public class MainMenuView : BaseView
    {
        // Sidebar buttons (UpperLayout)
        private readonly (By, string) _profileButtonLocator = (By.ID, "578d9b4e-0531-4cb3-abd7-aa79506c1b3e");
        private readonly (By, string) _notificationsButtonLocator = (By.ID, "6c66dc7b-5c51-4b1c-bd27-0814d9c837ae");
        private readonly (By, string) _eventsButtonLocator = (By.ID, "d5ac3302-135f-4d89-9af3-56df31776664");
        private readonly (By, string) _placesButtonLocator = (By.ID, "bcd4b7ed-97f9-419c-8df8-d8a0218388d2");
        private readonly (By, string) _communitiesButtonLocator = (By.ID, "9335caa1-070d-47cd-92f8-2ab0bee06003");
        private readonly (By, string) _mapButtonLocator = (By.ID, "2b8e4546-23be-4e65-973b-7928eb02f238");
        private readonly (By, string) _backpackButtonLocator = (By.ID, "bab6108c-7cce-45a1-9bcd-40412c1f435e");
        private readonly (By, string) _marketplaceButtonLocator = (By.ID, "31e1fb4b-d737-4351-bc21-97e00f715ebe");
        private readonly (By, string) _galleryButtonLocator = (By.ID, "6d5004d7-5a52-4250-b98a-5799f5e8c011");
        private readonly (By, string) _settingsButtonLocator = (By.ID, "e4146db9-0b45-4c41-8cf0-2cde69a0ce0a");
        private readonly (By, string) _controlsButtonLocator = (By.ID, "6f7c9619-29d4-4dfd-8aad-f8b10f56939a");
        private readonly (By, string) _helpButtonLocator = (By.ID, "c02afb7d-0abf-405e-9ecc-48f8cf439f42");
        private readonly (By, string) _sidebarSettingsButtonLocator = (By.ID, "a7a98fe6-eca1-4f67-996e-2049c9e020bb");

        public MainMenuView(DriverContainer driverContainer) : base(driverContainer)
        {
        }

        [AllureStep("Wait for main menu to be ready")]
        public void WaitForMenuReady(int timeoutSeconds = 20)
        {
            WaitForObject(_backpackButtonLocator, timeoutSeconds);
            Reporter.Log("Main menu is ready");
        }

        [AllureStep("Click Profile button")]
        public void ClickProfile()
        {
            ClickObject(_profileButtonLocator);
            Reporter.Log("Clicked Profile button");
        }

        [AllureStep("Click Notifications button")]
        public void ClickNotifications()
        {
            ClickObject(_notificationsButtonLocator);
            Reporter.Log("Clicked Notifications button");
        }

        [AllureStep("Click Events button")]
        public void ClickEvents()
        {
            ClickObject(_eventsButtonLocator);
            Reporter.Log("Clicked Events button");
        }

        [AllureStep("Click Places button")]
        public void ClickPlaces()
        {
            ClickObject(_placesButtonLocator);
            Reporter.Log("Clicked Places button");
        }

        [AllureStep("Click Communities button")]
        public void ClickCommunities()
        {
            ClickObject(_communitiesButtonLocator);
            Reporter.Log("Clicked Communities button");
        }

        [AllureStep("Click Map button")]
        public void ClickMap()
        {
            ClickObject(_mapButtonLocator);
            Reporter.Log("Clicked Map button");
        }

        [AllureStep("Click Backpack button")]
        public void ClickBackpack()
        {
            ClickObject(_backpackButtonLocator);
            Reporter.Log("Clicked Backpack button");
        }

        [AllureStep("Click Marketplace button")]
        public void ClickMarketplace()
        {
            ClickObject(_marketplaceButtonLocator);
            Reporter.Log("Clicked Marketplace button");
        }

        [AllureStep("Click Gallery button")]
        public void ClickGallery()
        {
            ClickObject(_galleryButtonLocator);
            Reporter.Log("Clicked Gallery button");
        }

        [AllureStep("Click Settings button")]
        public void ClickSettings()
        {
            ClickObject(_settingsButtonLocator);
            Reporter.Log("Clicked Settings button");
        }

        [AllureStep("Click Controls button")]
        public void ClickControls()
        {
            ClickObject(_controlsButtonLocator);
            Reporter.Log("Clicked Controls button");
        }

        [AllureStep("Click Help button")]
        public void ClickHelp()
        {
            ClickObject(_helpButtonLocator);
            Reporter.Log("Clicked Help button");
        }

        [AllureStep("Click Sidebar Settings button")]
        public void ClickSidebarSettings()
        {
            ClickObject(_sidebarSettingsButtonLocator);
            Reporter.Log("Clicked Sidebar Settings button");
        }
    }
}
