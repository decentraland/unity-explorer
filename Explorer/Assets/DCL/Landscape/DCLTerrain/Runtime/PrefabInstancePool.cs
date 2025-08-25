using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Decentraland.Terrain
{
    /// <remarks>
    ///     Why not <see cref="UnityEngine.Pool.ObjectPool{T}" />? Because it is not serializable and is not
    ///     preserved when reloading scripts during play mode. Also, crazy corner case: if this was a class,
    ///     and we used null to mean we don't need a pool, and that was in an array that is
    ///     script-reload-serializable, it would get deserialized to a zeroed out object instead of null,
    ///     because YAML can't have nulls.
    /// </remarks>
    [Serializable]
    public struct PrefabInstancePool : IDisposable
    {
        private List<GameObject> instances;
        private GameObject prefab;
#if UNITY_EDITOR
        private Transform parent;
#endif

        /// <param name="parent">
        ///     The transform under which to group all the instances of this pool. It
        ///     is only used in editor.
        /// </param>
        public PrefabInstancePool(GameObject prefab, Transform parent = null)
        {
            instances = new List<GameObject>();
            this.prefab = prefab;
#if UNITY_EDITOR
            this.parent = parent;
#endif
        }

        public void Dispose()
        {
            for (var i = 0; i < instances.Count; i++)
            {
                GameObject instance = instances[i];

                if (instance != null)
                    Object.Destroy(instances[i]);
            }
        }

        public GameObject Get()
        {
            GameObject item;

            if (instances.Count > 0)
            {
                int last = instances.Count - 1;
                item = instances[last];
                instances.RemoveAt(last);
            }
            else
            {
                item = Object.Instantiate(prefab
#if UNITY_EDITOR
                  , parent
#endif
                );

                item.name = prefab.name;
            }

            return item;
        }

        public bool IsCreated => prefab != null;

        public void Release(GameObject item)
        {
            instances.Add(item);
        }
    }
}
