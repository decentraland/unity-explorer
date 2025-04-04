using System;
using System.Collections.Generic;

namespace DCL.UI.SceneDebugConsole.LogHistory
{
    public interface ISceneDebugConsoleLogHistory
    {
        /// <summary>
        /// Event that is raised when a new log message is added to the history
        /// </summary>
        event Action<SceneDebugConsoleLogMessage> LogMessageAdded;

        /// <summary>
        /// Gets the list of log messages in the history
        /// </summary>
        IReadOnlyList<SceneDebugConsoleLogMessage> LogMessages { get; }

        /// <summary>
        /// Adds a new log message to the history
        /// </summary>
        /// <param name="logMessage">The log message to add</param>
        void AddLogMessage(SceneDebugConsoleLogMessage logMessage);

        /// <summary>
        /// Clears all log messages from the history
        /// </summary>
        void ClearLogMessages();
    }
}
