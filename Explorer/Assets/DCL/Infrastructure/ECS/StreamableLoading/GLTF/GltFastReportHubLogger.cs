using DCL.Diagnostics;
using GLTFast.Logging;
using System.Diagnostics;

namespace ECS.StreamableLoading.GLTF
{
    public class GltFastReportHubLogger : ICodeLogger
    {
        public LogCode LastErrorCode { get; private set; }

        public void Error(LogCode code, params string[] messages)
        {
            LastErrorCode = code;
            ReportHub.LogError(ReportCategory.GLTF_CONTAINER, LogMessages.GetFullMessage(code, messages));
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
            ReportHub.LogError(ReportCategory.GLTF_CONTAINER, message);
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
            ReportHub.LogWarning(ReportCategory.GLTF_CONTAINER, message);
        }

        [Conditional("UNITY_EDITOR")]
        private void LogVerbose(string message)
        {
            ReportHub.Log(ReportCategory.GLTF_CONTAINER, message);
        }
    }
}
