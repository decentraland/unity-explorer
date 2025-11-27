using DCL.Prefs;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// Detects non-graceful application exits and reports them to the analytics service
    /// </summary>
    public class CrashDetector : MonoBehaviour
    {
        public static void Initialize(IAnalyticsController analyticsController)
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.CRASH_DETECTOR_FLAG))
            {
                // Application crashed on previous run
                string previousSessionID = DCLPlayerPrefs.GetString(DCLPrefKeys.CRASH_DETECTOR_SESSION_ID, string.Empty);

                analyticsController.Track(AnalyticsEvents.General.CRASH, new JObject
                {
                    { "previous_session_id", previousSessionID }
                });
            }

            DCLPlayerPrefs.SetString(DCLPrefKeys.CRASH_DETECTOR_FLAG, string.Empty);
            DCLPlayerPrefs.SetString(DCLPrefKeys.CRASH_DETECTOR_SESSION_ID, analyticsController.SessionID);
            DCLPlayerPrefs.Save();

            var go = new GameObject("CrashDetector");
            go.AddComponent<CrashDetector>();
            DontDestroyOnLoad(go);
        }

        private void OnApplicationQuit()
        {
            // NOTE: If you remove this, make sure to call DCLPlayerPrefs.Save() in another OnApplicationQuit method
            DCLPlayerPrefs.DeleteKey(DCLPrefKeys.CRASH_DETECTOR_FLAG);
            DCLPlayerPrefs.Save();
        }
    }
}
