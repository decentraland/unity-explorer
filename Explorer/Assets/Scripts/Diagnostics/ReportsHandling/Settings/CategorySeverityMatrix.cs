using System;
using System.Collections.Generic;
using UnityEngine;

namespace Diagnostics.ReportsHandling
{
    [Serializable]
    internal class CategorySeverityMatrix : ICategorySeverityMatrix
    {
        [Serializable]
        public class Entry
        {
            public string Category;
            public LogType Severity;
        }

        // Remove an entry if category no longer exists
        [SerializeField] internal List<Entry> entries;

        public bool IsEnabled(string category, LogType severity) =>
            entries.Find(e => e.Category == category && e.Severity == severity) != null;
    }
}
