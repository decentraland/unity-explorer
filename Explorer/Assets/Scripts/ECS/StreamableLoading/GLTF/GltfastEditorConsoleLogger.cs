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
            Debug.LogError("PRAVS - " + LogMessages.GetFullMessage(code, messages));
        }

        public void Warning(LogCode code, params string[] messages)
        {
            LogWarning("PRAVS - " + LogMessages.GetFullMessage(code, messages));
        }

        public void Info(LogCode code, params string[] messages)
        {
            LogVerbose("PRAVS - " + LogMessages.GetFullMessage(code, messages));
        }

        public void Error(string message)
        {
            Debug.LogError("PRAVS - " + message);
        }

        public void Warning(string message)
        {
            LogWarning("PRAVS - " + message);
        }

        public void Info(string message)
        {
            LogVerbose("PRAVS - " + message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogWarning(string message)
        {
            Debug.LogWarning("PRAVS - " + message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogVerbose(string message)
        {
            Debug.Log("PRAVS - " + message);
        }
    }
}
