using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DCL.UI.SceneDebugConsole.Commands
{
    public class SceneDebugConsoleCommandsBus
    {
        private readonly Dictionary<string, CommandInfo> commands = new Dictionary<string, CommandInfo>();

        public event Action OnClearConsole;

        public SceneDebugConsoleCommandsBus()
        {
            RegisterDefaultCommands();
        }

        public string ExecuteCommand(string commandText)
        {
            if (string.IsNullOrWhiteSpace(commandText))
                return string.Empty;

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
            if (commands.TryGetValue(commandName, out CommandInfo commandInfo))
            {
                try
                {
                    return commandInfo.Action(args);
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
            commands[commandName] = new CommandInfo(description, action);
        }

        private void RegisterDefaultCommands()
        {
            // Help command
            RegisterCommand("help", "Shows the list of available commands", args =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Available commands:");

                // Add built-in clear command
                sb.AppendLine($"clear - Clears the console");

                // Add all other registered commands
                foreach (var cmd in commands.OrderBy(c => c.Key))
                {
                    sb.AppendLine($"{cmd.Key} - {cmd.Value.Description}");
                }

                return sb.ToString();
            });

            // Clear command (handled specially in ExecuteCommand for direct integration with the console)
            RegisterCommand("clear", "Clears the console", _ => string.Empty);

            // Echo command
            RegisterCommand("echo", "Prints the given text", args => string.Join(" ", args));

            // Version command
            RegisterCommand("version", "Shows the application version", _ => $"Application version: {Application.version}");

            // System info command
            RegisterCommand("sysinfo", "Shows system information", _ =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
                sb.AppendLine($"Device: {SystemInfo.deviceModel}");
                sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
                sb.AppendLine($"CPU: {SystemInfo.processorType}, {SystemInfo.processorCount} cores");
                sb.AppendLine($"Memory: {SystemInfo.systemMemorySize} MB");
                return sb.ToString();
            });

            // FPS command
            // RegisterCommand("fps", "Shows current FPS", _ => $"Current FPS: {Mathf.Round(1.0f / Time.smoothDeltaTime)}");

            // Time command
            /*RegisterCommand("time", "Shows time information", _ =>
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Time since startup: {Time.realtimeSinceStartup:F2} seconds");
                sb.AppendLine($"Current time scale: {Time.timeScale:F2}");
                sb.AppendLine($"Current time: {DateTime.Now}");
                return sb.ToString();
            });*/
        }

        private class CommandInfo
        {
            public string Description { get; }
            public Func<string[], string> Action { get; }

            public CommandInfo(string description, Func<string[], string> action)
            {
                Description = description;
                Action = action;
            }
        }
    }
}
