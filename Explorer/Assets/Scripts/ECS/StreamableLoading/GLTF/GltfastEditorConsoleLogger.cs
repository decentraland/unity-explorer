using GLTFast.Logging;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace ECS.StreamableLoading.GLTF
{
    public class GltfastEditorConsoleLogger : ICodeLogger
    {
        private const string GLTF_PREFIX = "GLTF - ";
        public LogCode LastErrorCode { get; private set; }

        public void Error(LogCode code, params string[] messages)
        {
            LastErrorCode = code;
            Debug.LogError(GLTF_PREFIX + LogMessages.GetFullMessage(code, messages));
        }

        public void Warning(LogCode code, params string[] messages)
        {
            LogWarning(GLTF_PREFIX + LogMessages.GetFullMessage(code, messages));
        }

        public void Info(LogCode code, params string[] messages)
        {
            LogVerbose(GLTF_PREFIX + LogMessages.GetFullMessage(code, messages));
        }

        public void Error(string message)
        {
            Debug.LogError(GLTF_PREFIX + message);
        }

        public void Warning(string message)
        {
            LogWarning(GLTF_PREFIX + message);
        }

        public void Info(string message)
        {
            LogVerbose(GLTF_PREFIX + message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogWarning(string message)
        {
            Debug.LogWarning(GLTF_PREFIX + message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogVerbose(string message)
        {
            Debug.Log(GLTF_PREFIX + message);
        }
    }
}
