using System;
using System.Collections.Generic;
using System.Linq;

namespace DCL.UI.SceneDebugConsole.Commands
{
    public class SimplifiedCommandsBus
    {
        private readonly Dictionary<string, Func<string[], string>> commands = new();

        public event Action OnClearConsole;

        public SimplifiedCommandsBus()
        {
            RegisterDefaultCommands();
        }

        public string ExecuteCommand(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return string.Empty;

            // Split command and arguments
            string[] parts = commandText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return string.Empty;

            string commandName = parts[0].ToLower();
            string[] args = parts.Skip(1).ToArray();

            // Handle built-in clear command
            if (commandName == "clear")
            {
                OnClearConsole?.Invoke();
                return string.Empty;
            }

            // Execute the command if it exists
            if (commands.TryGetValue(commandName, out var action))
            {
                try
                {
                    return action(args);
                }
                catch (Exception ex)
                {
                    return $"Error executing command '{commandName}': {ex.Message}";
                }
            }

            return $"Unknown command: '{commandName}'. Type 'help' for available commands.";
        }

        public void RegisterCommand(string commandName, string description, Func<string[], string> action)
        {
            commandName = commandName.ToLower();
            commands[commandName] = action;
        }

        private void RegisterDefaultCommands()
        {
            // Help command
            RegisterCommand("help", "Shows the list of available commands", args => "Available commands: help, clear, echo");

            // Clear command (handled specially in ExecuteCommand)
            RegisterCommand("clear", "Clears the console", _ => string.Empty);

            // Echo command
            RegisterCommand("echo", "Prints the given text", args => string.Join(" ", args));
        }
    }
}
