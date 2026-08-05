using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SceneLoadingScreens
{
    [CreateAssetMenu(menuName = "DCL/SceneLoadingScreens/Loading Tip Catalog", fileName = "LoadingTipCatalog")]
    public class LoadingTipCatalogSO : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public string Key;
            public TipView Prefab;
        }

        [SerializeField] private List<Entry> entries = new ();

        private Dictionary<string, TipView>? entriesByKey;

        public bool TryGet(string key, out TipView? prefab)
        {
            if (entriesByKey == null)
            {
                entriesByKey = new Dictionary<string, TipView>(StringComparer.OrdinalIgnoreCase);

                foreach (Entry entry in entries)
                    entriesByKey[entry.Key] = entry.Prefab;
            }

            return entriesByKey.TryGetValue(key, out prefab);
        }
    }
}
