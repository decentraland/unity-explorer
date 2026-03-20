namespace ExplorerAutomationTests.Views
{
    public class SplashScreenView : BaseView
    {
        private readonly (By, string) _splashScreenLocator = (By.NAME, "Splash(Clone)");

        public SplashScreenView(DriverContainer driverContainer) : base(driverContainer) { }

        [AllureStep("Check if splash screen is visible")]
        public bool IsScreenVisible()
        {
            return IsObjectPresent(_splashScreenLocator);
        }

        [AllureStep("Wait for loading screen to disappear")]
        public void WaitForSplashScreenToDisappear(int timeoutSeconds = 120)
        {
            WaitForObjectNotBePresent(_splashScreenLocator, timeoutSeconds);
            Reporter.Log("Loading screen has disappeared");
        }
    }
}