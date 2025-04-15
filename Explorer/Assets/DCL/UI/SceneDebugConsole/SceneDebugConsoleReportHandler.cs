// using DCL.UI.SceneDebugConsole.LogHistory;
// using DCL.UI.SceneDebugConsole.MessageBus;
// using UnityEngine;

namespace DCL.UI.SceneDebugConsole
{
    /// <summary>
    /// Captures Unity logs and sends them to the Scene Debug Console
    /// </summary>
    /*public class SceneDebugConsoleReportHandler : MonoBehaviour
    {
        private ISceneDebugConsoleMessageBus logMessagesBus;
        private SceneDebugConsoleSettings settings;

        public void Initialize(ISceneDebugConsoleMessageBus logMessagesBus, SceneDebugConsoleSettings settings)
        {
            this.logMessagesBus = logMessagesBus;
            this.settings = settings;

            if (settings.CaptureUnityLogs)
            {
                Application.logMessageReceived += OnLogMessageReceived;
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
        }

        private void OnLogMessageReceived(string logString, string stackTrace, LogType type)
        {
            if (logMessagesBus != null)
            {
                logMessagesBus.Send(logString, type, stackTrace);
            }
        }
    }*/
}
