using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    public class CategorySeverityMatrixOverride : ICategorySeverityMatrix
    {
        private readonly ICategorySeverityMatrix baseMatrix;
        private readonly Dictionary<(string, LogType), bool> overrideEntries;
        private Dictionary<(string, LogType), bool>? lookupCache;

        public CategorySeverityMatrixOverride(ICategorySeverityMatrix baseMatrix, List<CategorySeverityMatrixDto.MatrixEntryDto> overrideEntries)
        {
            this.baseMatrix = baseMatrix;
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
                    ReportHub.LogWarning(ReportCategory.ENGINE, $"Invalid severity '{entry.severity}' for category '{entry.category}' in log matrix override");
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

            // First, copy all base matrix entries
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

            // Then, add/override with JSON entries
            foreach (var (key, value) in overrideEntries)
            {
                lookupCache[key] = value;
            }
        }
    }
}
