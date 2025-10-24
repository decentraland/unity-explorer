using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class LogMatrixChatCommand : IChatCommand
    {
        public string Command => "log-matrix";
        public string Description => "<b>/log-matrix [enable|disable|toggle|clear|list] [category] [severity]</b>\n" +
                                   "  Control log matrix settings at runtime\n" +
                                   "  Examples:\n" +
                                   "    /log-matrix enable VOICE_CHAT Error\n" +
                                   "    /log-matrix disable SCENE_LOADING Warning\n" +
                                   "    /log-matrix toggle ENGINE Exception\n" +
                                   "    /log-matrix clear\n" +
                                   "    /log-matrix list";

        public bool DebugOnly => false;

        private readonly RuntimeReportsHandlingSettings runtimeSettings;

        public LogMatrixChatCommand(RuntimeReportsHandlingSettings runtimeSettings)
        {
            this.runtimeSettings = runtimeSettings;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length == 0) return false;
            
            string action = parameters[0].ToLower();
            return action switch
            {
                "enable" or "disable" or "toggle" => parameters.Length == 3,
                "clear" or "list" => parameters.Length == 1,
                _ => false
            };
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            string action = parameters[0].ToLower();

            return action switch
            {
                "enable" => ExecuteEnableCommand(parameters[1], parameters[2]),
                "disable" => ExecuteDisableCommand(parameters[1], parameters[2]),
                "toggle" => ExecuteToggleCommand(parameters[1], parameters[2]),
                "clear" => ExecuteClearCommand(),
                "list" => ExecuteListCommand(),
                _ => UniTask.FromResult("🔴 Invalid action. Use: enable, disable, toggle, clear, or list")
            };
        }

        private UniTask<string> ExecuteEnableCommand(string category, string severity)
        {
            if (!TryParseLogType(severity, out LogType logType))
                return UniTask.FromResult($"🔴 Invalid severity: {severity}. Use: Log, Warning, Error, Exception, Assert");

            runtimeSettings.GetDebugLogMatrix().EnableCategory(category, logType);
            return UniTask.FromResult($"🟢 Enabled {category}.{severity} logging");
        }

        private UniTask<string> ExecuteDisableCommand(string category, string severity)
        {
            if (!TryParseLogType(severity, out LogType logType))
                return UniTask.FromResult($"🔴 Invalid severity: {severity}. Use: Log, Warning, Error, Exception, Assert");

            runtimeSettings.GetDebugLogMatrix().DisableCategory(category, logType);
            return UniTask.FromResult($"🔴 Disabled {category}.{severity} logging");
        }

        private UniTask<string> ExecuteToggleCommand(string category, string severity)
        {
            if (!TryParseLogType(severity, out LogType logType))
                return UniTask.FromResult($"🔴 Invalid severity: {severity}. Use: Log, Warning, Error, Exception, Assert");

            runtimeSettings.GetDebugLogMatrix().ToggleCategory(category, logType);
            bool isEnabled = runtimeSettings.GetDebugLogMatrix().IsEnabled(category, logType);
            return UniTask.FromResult($"🔄 Toggled {category}.{severity} logging to {(isEnabled ? "enabled" : "disabled")}");
        }

        private UniTask<string> ExecuteClearCommand()
        {
            runtimeSettings.GetDebugLogMatrix().ClearOverrides();
            return UniTask.FromResult("🟢 Cleared all log matrix overrides");
        }

        private UniTask<string> ExecuteListCommand()
        {
            var overrides = runtimeSettings.GetDebugLogMatrix().GetOverrides();
            
            if (overrides.Count == 0)
                return UniTask.FromResult("📋 No log matrix overrides active");

            var sb = new StringBuilder();
            sb.AppendLine("📋 Active log matrix overrides:");
            
            var grouped = overrides.GroupBy(kvp => kvp.Key.Item1)
                                 .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"  {group.Key}:");
                foreach (var kvp in group.OrderBy(kvp => kvp.Key.Item2))
                {
                    string status = kvp.Value ? "✅" : "❌";
                    sb.AppendLine($"    {status} {kvp.Key.Item2}");
                }
            }

            return UniTask.FromResult(sb.ToString());
        }

        private static bool TryParseLogType(string severity, out LogType logType)
        {
            return (logType = severity.ToLower() switch
            {
                "log" => LogType.Log,
                "warning" => LogType.Warning,
                "error" => LogType.Error,
                "exception" => LogType.Exception,
                "assert" => LogType.Assert,
                _ => (LogType)(-1)
            }) != (LogType)(-1);
        }
    }
}
