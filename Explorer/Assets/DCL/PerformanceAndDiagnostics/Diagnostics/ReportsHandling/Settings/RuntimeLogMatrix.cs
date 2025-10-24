using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class RuntimeLogMatrix : ICategorySeverityMatrix
    {
        private readonly Dictionary<(string, LogType), bool> enabledEntries = new();
        private readonly ICategorySeverityMatrix baseMatrix;

        public RuntimeLogMatrix(ICategorySeverityMatrix baseMatrix)
        {
            this.baseMatrix = baseMatrix;
        }

        public bool IsEnabled(string category, LogType severity)
        {
            // Check runtime overrides first
            if (enabledEntries.TryGetValue((category, severity), out bool runtimeValue))
                return runtimeValue;

            // Fall back to base matrix
            return baseMatrix.IsEnabled(category, severity);
        }

        public void EnableCategory(string category, LogType severity)
        {
            enabledEntries[(category, severity)] = true;
            ReportHub.LogProductionInfo(string.Format(LogMatrixConstants.LOG_MATRIX_ENABLED, category, severity));
        }

        public void DisableCategory(string category, LogType severity)
        {
            enabledEntries[(category, severity)] = false;
            ReportHub.LogProductionInfo(string.Format(LogMatrixConstants.LOG_MATRIX_DISABLED, category, severity));
        }


        public IReadOnlyDictionary<(string, LogType), bool> GetOverrides() => enabledEntries;
    }
}
