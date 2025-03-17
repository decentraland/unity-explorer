using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Diagnostics
{
    [Serializable]
    internal class CategorySeverityMatrix : ICategorySeverityMatrix, ISerializationCallbackReceiver
    {
        [SerializeField] internal List<Entry>? entries;
        private Dictionary<(string, LogType), bool>? lookupCache;

        private void InitializeLookupCache()
        {
            lookupCache = new Dictionary<(string, LogType), bool>();
            foreach (var entry in entries)
            {
                lookupCache[(entry.Category, entry.Severity)] = true;
            }
        }

        public bool IsEnabled(string category, LogType severity)
        {
            if (lookupCache == null) InitializeLookupCache();
            return lookupCache!.TryGetValue((category, severity), out bool result) && result;
        }

        private void OnValidate()
        {
            lookupCache = null; // Invalidate the cache
        }

        [Serializable]
        public class Entry
        {
            public string Category;
            public LogType Severity;
        }

        public void OnBeforeSerialize() { this.OnValidate(); }

        public void OnAfterDeserialize() { }
    }
}
