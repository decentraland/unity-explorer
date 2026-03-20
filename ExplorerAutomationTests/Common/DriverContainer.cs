namespace ExplorerAutomationTests.Common
{
    public class DriverContainer
    {
        public AltDriver AltDriver { get; }
        public AppiumDriver<AppiumWebElement> AppiumDriver { get; }
        public IWebDriver SeleniumDriver { get; }

        public DriverContainer(
            AltDriver altDriver,
            AppiumDriver<AppiumWebElement> appiumDriver = null,
            IWebDriver seleniumDriver = null)
        {
            AltDriver = altDriver;
            AppiumDriver = appiumDriver;
            SeleniumDriver = seleniumDriver;
        }
    }
}