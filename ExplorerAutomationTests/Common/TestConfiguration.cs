namespace ExplorerAutomationTests.Common
{
    /// <summary>
    /// Centralized configuration for test execution
    /// Handles environment variables and provides default values for test settings
    /// </summary>
    public class TestConfiguration
    {
        // AltTester Configuration
        public static string AltTesterServerUrl = GetEnvironmentVariableOrDefault("ALT_TESTER_SERVER_URL", "127.0.0.1");
        public static int AltTesterServerPort = int.TryParse(GetEnvironmentVariableOrDefault("ALT_TESTER_SERVER_PORT", "13000"), out var port) ? port : 13000;
        public static string AltTesterAppName = GetEnvironmentVariableOrDefault("ALT_TESTER_APP_NAME", "__default__");
        public static int AltTesterConnectTimeout = int.TryParse(GetEnvironmentVariableOrDefault("ALT_TESTER_CONNECT_TIMEOUT", "60"), out var timeout) ? timeout : 60;

        // Platform Configuration
        public static PlatformType Platform = GetPlatformType();
        public static string DeviceName = GetEnvironmentVariableOrDefault("DEVICE_NAME", "android");
        public static string AppBundleId = GetEnvironmentVariableOrDefault("APP_BUNDLE_ID", "com.example.app");

        // Driver Configuration
        public static bool RunningWithAppium = GetEnvironmentVariableOrDefault("RUN_TESTS_WITH_APPIUM", "false") == "true";
        public static bool RunningWithSelenium = GetEnvironmentVariableOrDefault("RUN_TESTS_WITH_SELENIUM", "false") == "true";

        // WebGL Configuration
        public static string WebGLUrl = GetEnvironmentVariableOrDefault("WEBGL_URL", "https://example.com/game");

        private static PlatformType GetPlatformType()
        {
            string platformStr = GetEnvironmentVariableOrDefault("TEST_PLATFORM", "Android");
            return Enum.TryParse<PlatformType>(platformStr, true, out var result) ? result : PlatformType.Android;
        }

        /// <summary>
        /// Gets an environment variable value or returns a default value if not set
        /// </summary>
        /// <param name="variableName">Name of the environment variable</param>
        /// <param name="defaultValue">Default value to return if variable is not set</param>
        /// <returns>Environment variable value or default value</returns>
        public static string GetEnvironmentVariableOrDefault(string variableName, string defaultValue)
        {
            string value = Environment.GetEnvironmentVariable(variableName) ?? string.Empty;
            return string.IsNullOrEmpty(value) ? defaultValue : value;
        }
    }

    /// <summary>
    /// Supported platforms for test execution
    /// </summary>
    public enum PlatformType    {
        Android,
        iOS,
        WebGL
    }
}
