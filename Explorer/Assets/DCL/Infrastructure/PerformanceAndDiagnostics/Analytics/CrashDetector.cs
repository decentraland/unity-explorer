using Segment.Serialization;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    /// <summary>
    /// Detects non-graceful application exits and reports them to the analytics service
    /// </summary>
    public class CrashDetector : MonoBehaviour
    {
        private const string PREFS_FLAG = "CrashDetector.flag";
        private const string PREFS_SESSION_ID = "CrashDetector.sessionID";

        public static void Initialize(IAnalyticsController analyticsController)
        {
            if (PlayerPrefs.HasKey(PREFS_FLAG))
            {
                // Application crashed on previous run
                string previousSessionID = PlayerPrefs.GetString(PREFS_SESSION_ID, string.Empty);

                analyticsController.Track(AnalyticsEvents.General.CRASH, new JsonObject
                {
                    { "previous_session_id", previousSessionID }
                });
            }

            PlayerPrefs.SetString(PREFS_FLAG, string.Empty);
            PlayerPrefs.SetString(PREFS_SESSION_ID, analyticsController.SessionID);
            PlayerPrefs.Save();

            var go = new GameObject("CrashDetector");
            go.AddComponent<CrashDetector>();
            DontDestroyOnLoad(go);
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.DeleteKey(PREFS_FLAG);
            PlayerPrefs.Save();
        }
    }
}
