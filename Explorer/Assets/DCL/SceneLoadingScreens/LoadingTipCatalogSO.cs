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

        public bool TryGet(string key, out TipView? prefab)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];

                if (entry.Key == key)
                {
                    prefab = entry.Prefab;
                    return true;
                }
            }

            prefab = null;
            return false;
        }
    }
}
