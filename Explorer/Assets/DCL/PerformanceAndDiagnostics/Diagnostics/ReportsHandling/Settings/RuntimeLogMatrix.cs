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
            ReportHub.LogProductionInfo($"Enabled {category}.{severity} logging");
        }

        public void DisableCategory(string category, LogType severity)
        {
            enabledEntries[(category, severity)] = false;
            ReportHub.LogProductionInfo($"Disabled {category}.{severity} logging");
        }

        public void ToggleCategory(string category, LogType severity)
        {
            if (enabledEntries.TryGetValue((category, severity), out bool currentValue))
            {
                enabledEntries[(category, severity)] = !currentValue;
                ReportHub.LogProductionInfo($"Toggled {category}.{severity} logging to {!currentValue}");
            }
            else
            {
                // Not explicitly set, check base matrix and invert
                bool baseValue = baseMatrix.IsEnabled(category, severity);
                enabledEntries[(category, severity)] = !baseValue;
                ReportHub.LogProductionInfo($"Toggled {category}.{severity} logging to {!baseValue}");
            }
        }

        public void ClearOverrides()
        {
            enabledEntries.Clear();
            ReportHub.LogProductionInfo("Cleared all log matrix overrides");
        }

        public Dictionary<(string, LogType), bool> GetOverrides() => new(enabledEntries);
    }
}
