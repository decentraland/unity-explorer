using PlatformType = ExplorerAutomationTests.Common.PlatformType;

namespace ExplorerAutomationTests.Tests
{
    /// <summary>
    /// Base test class that handles driver setup, teardown, and common test infrastructure
    /// All test classes should inherit from this class to get consistent test behavior
    /// </summary>
    [TestFixture]
    [AllureNUnit]
    public class BaseTest
    {
        protected Exception ExceptionFromOneTimeSetUp;
        protected DriverContainer Drivers { get; set; } = default;
        
        // View instances available to all test classes
        protected AuthenticationMainScreenView AuthenticationMainScreenView { get; set; }
        protected SplashScreenView SplashScreenView { get; set; }
        protected LoadingScreenView LoadingScreenView { get; set; }
        protected MainMenuView MainMenuView { get; set; }
        protected ExplorePanelView ExplorePanelView { get; set; }

        private static Dictionary<String, String> unityLogs = new Dictionary<String, String>();


        #region Setup and Teardown

        [OneTimeSetUp]
        [AllureBefore("Initialize Test Configuration and Start All Drivers")]
        public void OneTimeSetUp()
        {
            Reporter.Log("OneTimeSetUp - Initializing Test Configuration and Starting All Drivers");
            try
            {
                StartAllDrivers();
                SetupUnityLogListener();
                InitializeViews();
            }
            catch (Exception ex)
            {
                ExceptionFromOneTimeSetUp = ex;
                Reporter.Log("Exception during OneTimeSetUp: " + ex.Message);
                Reporter.Log("Stack Trace: " + ex.StackTrace);
            }
        }

        [OneTimeTearDown]
        [AllureAfter("Stop All Drivers and Clean Up")]
        public void OneTimeTearDown()
        {
            AddUnityLogsToAllure();
            StopAllDrivers();
            Reporter.Log("All drivers stopped and cleanup completed.");
        }

        [SetUp]
        [AllureBefore("Set up before each test")]
        public void SetUp()
        {
            if (ExceptionFromOneTimeSetUp != null)
            {
                throw ExceptionFromOneTimeSetUp;
            }

            // Add test-specific setup here if needed
            Reporter.Log($"Starting test: {TestContext.CurrentContext.Test.Name}");
        }

        [TearDown]
        [AllureAfter("Clean up after each test")]
        public void TearDown()
        {
            var testResult = TestContext.CurrentContext.Result.Outcome.Status;
            Reporter.Log($"Test {TestContext.CurrentContext.Test.Name} completed with status: {testResult}");

            if (testResult == NUnit.Framework.Interfaces.TestStatus.Failed)
            {
                Reporter.Log("Test failed - taking screenshot for debugging");
                Reporter.TakeScreenshot("" + TestContext.CurrentContext.Test.Name + "_Failed");
            }
        }

        #endregion

        #region Driver Management

        [AllureStep("Initialize View Objects")]
        public void InitializeViews()
        {
            Reporter.Log("Initializing view objects...");
            
            AuthenticationMainScreenView = new AuthenticationMainScreenView(Drivers);
            SplashScreenView = new SplashScreenView(Drivers);
            LoadingScreenView = new LoadingScreenView(Drivers);
            MainMenuView = new MainMenuView(Drivers);
            ExplorePanelView = new ExplorePanelView(Drivers);

            Reporter.Log("All view objects initialized successfully");
        }

        [AllureStep("Start All Drivers and Set Up Test Environment")]
        public void StartAllDrivers()
        {
            Reporter.Log("Setting up test environment...");
            Reporter.Log($"Platform: {TestConfiguration.Platform}");
            Reporter.Log($"Running tests with Appium: {TestConfiguration.RunningWithAppium}");
            Reporter.Log($"Running tests with Selenium: {TestConfiguration.RunningWithSelenium}");

            // Start optional drivers based on configuration
            AppiumDriver<AppiumWebElement> appiumDriver = null;
            IWebDriver seleniumDriver = null;

            if (TestConfiguration.RunningWithAppium)
            {
                appiumDriver = StartAppiumDriver();
            }

            if (TestConfiguration.RunningWithSelenium)
            {
                seleniumDriver = StartSeleniumDriver();
            }

            // Create driver container
            var altDriver = StartAltTesterDriver();

            Drivers = new DriverContainer(altDriver, appiumDriver, seleniumDriver);

            Reporter.Log("All drivers started successfully");

        }

        [AllureStep("Start AltTester Driver")]
        public AltDriver StartAltTesterDriver()
        {

            Reporter.Log($"Connecting to AltTester at {TestConfiguration.AltTesterServerUrl}:{TestConfiguration.AltTesterServerPort}");

            var driver = new AltDriver(
                host: TestConfiguration.AltTesterServerUrl,
                port: TestConfiguration.AltTesterServerPort,
                appName: TestConfiguration.AltTesterAppName,
                enableLogging: false,
                connectTimeout: 5
            );

            Reporter.AltDriver = driver;

            Reporter.Log($"Successfully connected to the game.");

            return driver;
        }

        [AllureStep("Start Appium Driver")]
        protected virtual AppiumDriver<AppiumWebElement> StartAppiumDriver()
        {
            Reporter.Log("Setting up Appium driver...");

            var appiumOptions = new AppiumOptions();
            AppiumDriver<AppiumWebElement> driver = null;

            switch (TestConfiguration.Platform)
            {
                case PlatformType.Android:
                    appiumOptions.AddAdditionalCapability("platformName", "Android");
                    appiumOptions.AddAdditionalCapability("appium:automationName", "UiAutomator2");
                    appiumOptions.AddAdditionalCapability("appium:newCommandTimeout", 2000);
                    appiumOptions.AddAdditionalCapability("appium:autoGrantPermissions", true);
                    appiumOptions.AddAdditionalCapability("deviceName", TestConfiguration.DeviceName);
                    appiumOptions.AddAdditionalCapability("appPackage", TestConfiguration.AppBundleId);
                    driver = new AndroidDriver<AppiumWebElement>(new Uri("http://127.0.0.1:4723/wd/hub"), appiumOptions);
                    break;

                case PlatformType.iOS:
                    appiumOptions.AddAdditionalCapability("platformName", "iOS");
                    appiumOptions.AddAdditionalCapability("deviceName", TestConfiguration.DeviceName);
                    appiumOptions.AddAdditionalCapability("bundleId", TestConfiguration.AppBundleId);
                    driver = new IOSDriver<AppiumWebElement>(new Uri("http://127.0.0.1:4723/wd/hub"), appiumOptions);
                    break;

                default:
                    Reporter.Log("Appium not supported for current platform");
                    break;
            }

            return driver;
        }

        [AllureStep("Start Selenium Driver")]
        protected virtual IWebDriver StartSeleniumDriver()
        {

            if (TestConfiguration.Platform == PlatformType.WebGL)
            {
                Reporter.Log("Setting up Chrome driver for WebGL testing...");

                var chromeOptions = new ChromeOptions();
                chromeOptions.AddArgument("--no-sandbox");
                chromeOptions.AddArgument("--disable-dev-shm-usage");

                var driver = new ChromeDriver(chromeOptions);
                driver.Navigate().GoToUrl(TestConfiguration.WebGLUrl);

                return driver;
            }

            Reporter.Log("Selenium not needed for current platform");
            return null;
        }

        protected virtual void StopAllDrivers()
        {
            try
            {
                Drivers.AltDriver.Stop();
                Drivers.SeleniumDriver.Quit();
                Drivers.AppiumDriver.Quit();

                Reporter.Log("All drivers stopped successfully");
            }
            catch (Exception ex)
            {
                Reporter.Log($"Error stopping drivers: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        protected virtual void SetupUnityLogListener()
        {
            if (Drivers.AltDriver != null)
            {
                Reporter.Log("Setting up Unity log listener");
                Drivers.AltDriver.AddNotificationListener<AltLogNotificationResultParams>(
                    NotificationType.LOG,
                    LogCallback,
                    true
                );
            }
        }

        protected virtual void LogCallback(AltLogNotificationResultParams logParams)
        {
            var projectDirectory = Directory.GetCurrentDirectory();
            var logDirectory = Path.Combine(projectDirectory, "screenshots");

            if (!Directory.Exists(logDirectory))
                Directory.CreateDirectory(logDirectory);

            var testName = TestContext.CurrentContext.Test.Name;
            var filename = testName + "-UnityLogs.txt";
            var filepath = Path.Combine(logDirectory, filename);

            var log = logParams;

            // Log all Unity messages regardless of level
            using (var sw = new StreamWriter(filepath, true))
            {
                sw.WriteLine($"{log.message}");
                sw.WriteLine($"StackTrace : {log.stackTrace}");
                sw.WriteLine(log);
            }
            unityLogs.TryAdd(filename, filepath);
        }

        public static void AddUnityLogsToAllure()
        {
            foreach (var item in unityLogs)
            {
                var attachmentName = TestContext.CurrentContext.Test.Name + "-" + item.Key;
                try
                {
                    Reporter.AttachFileToAllure(item.Value, attachmentName);
                }
                catch (Exception)
                {
                    Reporter.Log("No Unity logs found.");
                }
                unityLogs.Remove(item.Key);
            }
        }

        #endregion
    }
}