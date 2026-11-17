using UnityEngine;

namespace DCL.Diagnostics
{
    /// <summary>
    ///     One report as a sink receives it: the message text plus the category and scene the reporter attached,
    ///     which the Unity log line itself does not carry.
    /// </summary>
    public readonly struct ReportLogEntry
    {
        public readonly LogType LogType;
        public readonly string Category;
        public readonly SceneShortInfo Scene;
        public readonly string Message;
        public readonly string? StackTrace;

        public ReportLogEntry(LogType logType, string category, SceneShortInfo scene, string message, string? stackTrace)
        {
            LogType = logType;
            Category = category;
            Scene = scene;
            Message = message;
            StackTrace = stackTrace;
        }
    }
}
