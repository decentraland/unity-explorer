using DCL.Prefs;
using DCL.Utility;
using Newtonsoft.Json.Linq;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// Detects non-graceful application exits and reports them to the analytics service
    /// </summary>
    public static class CrashDetector
    {
        public static void Initialize(IAnalyticsController analyticsController)
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.CRASH_DETECTOR_FLAG))
            {
                // Application crashed on previous run
                string previousSessionID = DCLPlayerPrefs.GetString(DCLPrefKeys.CRASH_DETECTOR_SESSION_ID, string.Empty);

                analyticsController.Track(AnalyticsEvents.General.CRASH, new JObject
                {
                    { "previous_session_id", previousSessionID },
                });
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CRASH_DETECTOR_FLAG, string.Empty);
            DCLPlayerPrefs.SetString(DCLPrefKeys.CRASH_DETECTOR_SESSION_ID, analyticsController.SessionID);
            DCLPlayerPrefs.Save();

            ExitUtils.UnregisterCleanUpCandidate(nameof(CrashDetector));
            ExitUtils.RegisterCleanUpCandidate(new OnQuittingCleanUpCandidate(nameof(CrashDetector), ClearCrashFlag));
        }

        private static void ClearCrashFlag()
        {
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.CRASH_DETECTOR_FLAG);
        }
    }
}
