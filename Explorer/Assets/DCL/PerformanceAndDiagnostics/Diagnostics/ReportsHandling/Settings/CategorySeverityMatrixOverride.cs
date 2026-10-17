using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class CategorySeverityMatrixOverride : ICategorySeverityMatrix
    {
        private readonly ICategorySeverityMatrix baseMatrix;
        private readonly Dictionary<(string, LogType), bool> overrideEntries;
        private readonly bool isOverrideMode;
        private Dictionary<(string, LogType), bool>? lookupCache;

        public CategorySeverityMatrixOverride(ICategorySeverityMatrix baseMatrix, List<CategorySeverityMatrixDto.MatrixEntryDto> overrideEntries, bool isOverrideMode = false)
        {
            this.baseMatrix = baseMatrix;
            this.isOverrideMode = isOverrideMode;
            this.overrideEntries = new Dictionary<(string, LogType), bool>();

            foreach (var entry in overrideEntries)
            {
                try
                {
                    var logType = CategorySeverityMatrixDto.MatrixEntryDto.StringToLogType(entry.severity);
                    this.overrideEntries[(entry.category, logType)] = true;
                }
                catch (ArgumentException)
                {
                    ReportHub.LogWarning(ReportCategory.ENGINE, string.Format(LogMatrixConstants.LOG_MATRIX_INVALID_SEVERITY, entry.severity, entry.category));
                }
            }
        }

        public bool IsEnabled(string category, LogType severity)
        {
            if (lookupCache == null) 
                InitializeLookupCache();

            return lookupCache!.TryGetValue((category, severity), out bool result) && result;
        }

        private void InitializeLookupCache()
        {
            lookupCache = new Dictionary<(string, LogType), bool>();

            if (isOverrideMode)
            {
                foreach (var (key, value) in overrideEntries)
                {
                    lookupCache[key] = value;
                }
            }
            else
            {
                if (baseMatrix is CategorySeverityMatrix baseMatrixImpl)
                {
                    if (baseMatrixImpl.entries != null)
                    {
                        foreach (var entry in baseMatrixImpl.entries)
                        {
                            lookupCache[(entry.Category, entry.Severity)] = true;
                        }
                    }
                }

                foreach (var (key, value) in overrideEntries)
                {
                    lookupCache[key] = value;
                }
            }
        }
    }
}
