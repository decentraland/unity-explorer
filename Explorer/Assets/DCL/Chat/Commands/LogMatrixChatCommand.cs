using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DCL.Chat.Commands
{
    public class LogMatrixChatCommand : IChatCommand
    {
        public string Command => "log-matrix";
        public string Description => "<b>/log-matrix [enable|disable] [category] [severity]</b>\n" +
                                   "  Control log matrix settings at runtime\n" +
                                   "  Examples:\n" +
                                   "    /log-matrix enable VOICE_CHAT Error\n" +
                                   "    /log-matrix disable SCENE_LOADING Warning";

        public bool DebugOnly => false;

        private readonly RuntimeReportsHandlingSettings runtimeSettings;

        private static readonly string[] VALID_ACTIONS = { "enable", "disable" };
        private static readonly string[] VALID_SEVERITIES = { "log", "warning", "error", "exception", "assert" };
        private static readonly LogType[] LOG_TYPES = { LogType.Log, LogType.Warning, LogType.Error, LogType.Exception, LogType.Assert };

        public LogMatrixChatCommand(RuntimeReportsHandlingSettings runtimeSettings)
        {
            this.runtimeSettings = runtimeSettings;
        }

        public bool ValidateParameters(string[] parameters)
        {
            if (parameters.Length != 3) return false;
            
            string action = parameters[0];
            return IsValidAction(action);
        }

        public UniTask<string> ExecuteCommandAsync(string[] parameters, CancellationToken ct)
        {
            string action = parameters[0];
            
            return action switch
            {
                "enable" => ExecuteEnableCommand(parameters[1], parameters[2]),
                "disable" => ExecuteDisableCommand(parameters[1], parameters[2]),
                _ => UniTask.FromResult(LogMatrixConstants.LOG_MATRIX_INVALID_ACTION)
            };
        }

        private UniTask<string> ExecuteEnableCommand(string category, string severity)
        {
            if (!TryParseLogType(severity, out LogType logType))
                return UniTask.FromResult(string.Format(LogMatrixConstants.LOG_MATRIX_INVALID_SEVERITY_CMD, severity));

            runtimeSettings.GetDebugLogMatrix().EnableCategory(category, logType);
            return UniTask.FromResult(string.Format(LogMatrixConstants.LOG_MATRIX_ENABLED_CMD, category, severity));
        }

        private UniTask<string> ExecuteDisableCommand(string category, string severity)
        {
            if (!TryParseLogType(severity, out LogType logType))
                return UniTask.FromResult(string.Format(LogMatrixConstants.LOG_MATRIX_INVALID_SEVERITY_CMD, severity));

            runtimeSettings.GetDebugLogMatrix().DisableCategory(category, logType);
            return UniTask.FromResult(string.Format(LogMatrixConstants.LOG_MATRIX_DISABLED_CMD, category, severity));
        }



        private static bool IsValidAction(string action)
        {
            for (int i = 0; i < VALID_ACTIONS.Length; i++)
            {
                if (string.Equals(action, VALID_ACTIONS[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static bool TryParseLogType(string severity, out LogType logType)
        {
            for (int i = 0; i < VALID_SEVERITIES.Length; i++)
            {
                if (string.Equals(severity, VALID_SEVERITIES[i], StringComparison.OrdinalIgnoreCase))
                {
                    logType = LOG_TYPES[i];
                    return true;
                }
            }
            
            logType = (LogType)(-1);
            return false;
        }
    }
}
