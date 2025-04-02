using System;

namespace DCL.UI.SceneDebugConsole.Commands
{
    public interface ISceneDebugConsoleCommandsBus
    {
        /// <summary>
        /// Event that is raised when a clear command is executed
        /// </summary>
        event Action OnClearConsole;

        /// <summary>
        /// Executes a command with the given input text
        /// </summary>
        /// <param name="commandText">The command text to execute</param>
        /// <returns>The result of the command execution</returns>
        string ExecuteCommand(string commandText);

        /// <summary>
        /// Registers a new command with the command bus
        /// </summary>
        /// <param name="commandName">The name of the command</param>
        /// <param name="description">A description of what the command does</param>
        /// <param name="action">The action to execute when the command is invoked</param>
        void RegisterCommand(string commandName, string description, Func<string[], string> action);
    }
}
