
namespace ExplorerAutomationTests.Tests
{
    [TestFixture]
    [AllureNUnit]
    public class BaseTest
    {
        protected Exception ExceptionFromOneTimeSetUp;
        protected AltDriver AltDriver { get; set; }

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
                StartDriver();
                SetupUnityLogListener();
                InitializeViews();
                EnsureInWorld();
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
            StopDriver();
            Reporter.Log("Driver stopped and cleanup completed.");
        }

        [SetUp]
        [AllureBefore("Set up before each test")]
        public void SetUp()
        {
            if (ExceptionFromOneTimeSetUp != null)
            {
                throw ExceptionFromOneTimeSetUp;
            }

            Reporter.Log($"Starting test: {TestContext.CurrentContext.Test.Name}");
            
            // In case a popup is opened, this will close it
            PressEscape();
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

            AuthenticationMainScreenView = new AuthenticationMainScreenView(AltDriver);
            SplashScreenView = new SplashScreenView(AltDriver);
            LoadingScreenView = new LoadingScreenView(AltDriver);
            MainMenuView = new MainMenuView(AltDriver);
            ExplorePanelView = new ExplorePanelView(AltDriver);

            Reporter.Log("All view objects initialized successfully");
        }

        [AllureStep("Start AltTester Driver")]
        public void StartDriver()
        {
            Reporter.Log($"Connecting to AltTester at 127.0.0.1:13000");

            AltDriver = new AltDriver(
                host: "127.0.0.1",
                port: 13000,
                appName: "__default__",
                enableLogging: false,
                connectTimeout: 5
            );

            Reporter.AltDriver = AltDriver;
            Reporter.Log("Successfully connected to the game.");
        }

        protected virtual void StopDriver()
        {
            try
            {
                AltDriver?.Stop();
                Reporter.Log("Driver stopped successfully");
            }
            catch (Exception ex)
            {
                Reporter.Log($"Error stopping driver: {ex.Message}");
            }
        }

        #endregion

        #region In-World Setup

        [AllureStep("Ensure player is in-world")]
        protected virtual void EnsureInWorld()
        {
            if (SplashScreenView.IsScreenVisible() || AuthenticationMainScreenView.IsScreenVisible())
            {
                Reporter.Log("Authentication / splash screen detected — entering world");
                SplashScreenView.WaitForSplashScreenToDisappear();
                AuthenticationMainScreenView.WaitForScreenReady();
                AuthenticationMainScreenView.ClickJumpIntoWorld();
                LoadingScreenView.WaitForLoadingComplete();
            }
            else
            {
                Reporter.Log("Already in-world — skipping authentication");
            }

            MainMenuView.WaitForMenuReady();
            Reporter.Log("Player is in-world and main menu is ready");
        }

        #endregion

        #region Input Helpers

        [AllureStep("Press key")]
        public void PressKey(AltKeyCode keyCode, float power = 1, float duration = 0.1f)
        {
            Reporter.Log($"Pressing key: {keyCode}");
            AltDriver.PressKey(keyCode, power, duration);
            Thread.Sleep(500);
        }

        [AllureStep("Press Escape")]
        public void PressEscape()
        {
            PressKey(AltKeyCode.Escape);
        }

        #endregion

        #region Utility Methods

        protected virtual void SetupUnityLogListener()
        {
            if (AltDriver != null)
            {
                Reporter.Log("Setting up Unity log listener");
                AltDriver.AddNotificationListener<AltLogNotificationResultParams>(
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
