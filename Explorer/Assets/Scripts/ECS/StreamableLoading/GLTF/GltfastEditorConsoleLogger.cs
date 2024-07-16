using GLTFast.Logging;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace ECS.StreamableLoading.GLTF
{
    public class GltfastEditorConsoleLogger : ICodeLogger
    {
        public LogCode LastErrorCode { get; private set; }

        public void Error(LogCode code, params string[] messages)
        {
            LastErrorCode = code;
            Debug.LogError(LogMessages.GetFullMessage(code, messages));
        }

        public void Warning(LogCode code, params string[] messages)
        {
            LogWarning(LogMessages.GetFullMessage(code, messages));
        }

        public void Info(LogCode code, params string[] messages)
        {
            LogVerbose(LogMessages.GetFullMessage(code, messages));
        }

        public void Error(string message)
        {
            Debug.LogError(message);
        }

        public void Warning(string message)
        {
            LogWarning(message);
        }

        public void Info(string message)
        {
            LogVerbose(message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogVerbose(string message)
        {
            Debug.Log(message);
        }
    }
}
