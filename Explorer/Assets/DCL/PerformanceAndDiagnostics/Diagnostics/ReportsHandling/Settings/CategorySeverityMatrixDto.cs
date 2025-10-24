using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    [Serializable]
    public class CategorySeverityMatrixDto
    {
        [SerializeField] public bool override = false;
        [SerializeField] public List<MatrixEntryDto> debugLogMatrix = new();
        [SerializeField] public List<MatrixEntryDto> sentryMatrix = new();

        [Serializable]
        public class MatrixEntryDto
        {
            public string category;
            public string severity;

            public MatrixEntryDto() { }

            public MatrixEntryDto(string category, string severity)
            {
                this.category = category;
                this.severity = severity;
            }

            public MatrixEntryDto(string category, LogType severity)
            {
                this.category = category;
                this.severity = LogTypeToString(severity);
            }

            public static string LogTypeToString(LogType logType) => logType switch
            {
                LogType.Error => "Error",
                LogType.Assert => "Assert", 
                LogType.Warning => "Warning",
                LogType.Log => "Log",
                LogType.Exception => "Exception",
                _ => throw new ArgumentOutOfRangeException(nameof(logType), logType, null)
            };

            public static LogType StringToLogType(string severity) => severity switch
            {
                "Error" => LogType.Error,
                "Assert" => LogType.Assert,
                "Warning" => LogType.Warning,
                "Log" => LogType.Log,
                "Exception" => LogType.Exception,
                _ => throw new ArgumentException($"Invalid severity: {severity}", nameof(severity))
            };
        }
    }
}
