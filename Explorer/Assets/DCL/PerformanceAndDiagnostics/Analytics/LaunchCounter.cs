using DCL.Prefs;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    ///     Tracks how many times the application has been launched
    /// </summary>
    public static class LaunchCounter
    {
        public static int Count { get; private set; }

        public static void Increment()
        {
            Count = DCLPlayerPrefs.GetInt(DCLPrefKeys.LAUNCH_COUNT) + 1;
            DCLPlayerPrefs.SetInt(DCLPrefKeys.LAUNCH_COUNT, Count, save: true);
        }
    }
}
