
namespace ExplorerAutomationTests.Views
{
    public class LoadingScreenView : BaseView
    {
        private readonly (By, string) _sceneLoadingScreenLocator = (By.ID, "21e9d696-d866-4717-85c0-2b6e4f1c4d9d");

        public LoadingScreenView(AltDriver altDriver) : base(altDriver)
        {
        }

        [AllureStep("Wait for loading screen to appear")]
        public void WaitForLoadingScreenVisible(int timeoutSeconds = 30)
        {
            WaitForObject(_sceneLoadingScreenLocator, timeoutSeconds);
            Reporter.Log("Loading screen is visible");
        }

        [AllureStep("Wait for loading screen to disappear")]
        public void WaitForLoadingScreenToDisappear(int timeoutSeconds = 120)
        {
            WaitForObjectNotBePresent(_sceneLoadingScreenLocator, timeoutSeconds);
            Reporter.Log("Loading screen has disappeared");
        }

        [AllureStep("Wait for loading to complete")]
        public void WaitForLoadingComplete(int appearTimeoutSeconds = 30, int disappearTimeoutSeconds = 120)
        {
            WaitForLoadingScreenVisible(appearTimeoutSeconds);
            WaitForLoadingScreenToDisappear(disappearTimeoutSeconds);
            Reporter.Log("Loading complete");
        }
    }
}
