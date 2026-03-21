
namespace ExplorerAutomationTests.Views
{
    public class AuthenticationMainScreenView : BaseView
    {
        private readonly (By, string) _screenLocator = (By.NAME, "Authentication.MainScreen(Clone)");
        private readonly (By, string) _existingAccountScreenLocator = (By.NAME, "Lobby.ExistingAccount.Screen");
        private readonly (By, string) _jumpIntoWorldButtonLocator = (By.ID, "646623d5-3519-49df-93ed-ab668d7917db");
        private readonly (By, string) _useADifferentAccountButtonLocator = (By.ID, "f658ab9f-18ac-4281-a0a9-dd030d8224d6");

        public AuthenticationMainScreenView(AltDriver altDriver) : base(altDriver)
        {
        }

        [AllureStep("Wait for authentication main screen to be ready")]
        public void WaitForScreenReady(int timeoutSeconds = 20)
        {
            WaitForObject(_screenLocator, timeoutSeconds);
            WaitForObject(_jumpIntoWorldButtonLocator, timeoutSeconds);
            Reporter.Log("Authentication main screen is ready");
        }

        [AllureStep("Check if authentication main screen is visible")]
        public bool IsScreenVisible()
        {
            return IsObjectPresent(_screenLocator);
        }

        [AllureStep("Click Jump Into Decentraland button")]
        public void ClickJumpIntoWorld()
        {
            ClickObject(_jumpIntoWorldButtonLocator);
            Reporter.Log("Clicked 'Jump Into Decentraland' button");
        }

        [AllureStep("Click Use A Different Account button")]
        public void ClickUseADifferentAccount()
        {
            ClickObject(_useADifferentAccountButtonLocator);
            Reporter.Log("Clicked 'Use A Different Account' button");
        }
    }
}
