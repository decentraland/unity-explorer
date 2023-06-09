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

        // TODO cache results and invalidate only when entries are changed
        public bool IsEnabled(string category, LogType severity)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry.Category == category && entry.Severity == severity)
                    return true;
            }

            return false;
        }
    }
}
