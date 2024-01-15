using DCL.ECSComponents;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.NftShape.Frame
{
    [CreateAssetMenu(fileName = "NftShape Settings", menuName = "SDKComponents/NftShape/Settings", order = 0)]
    public class NftShapeSettings : ScriptableObject
    {
        [SerializeField] private List<Pair> pairs = new ();
        [SerializeField] private GameObject defaultFrame = null!;

        public IReadOnlyDictionary<NftFrameType, GameObject> FramePrefabs()
        {
            return pairs.ToDictionary(e => e.nftFrameType, e => e.prefab.EnsureNotNull());
        }

        public GameObject DefaultFrame()
        {
            return defaultFrame.EnsureNotNull("Default frame not set");
        }

        [ContextMenu(nameof(Ensure))]
        public void Ensure()
        {
            var map = FramePrefabs();
            string[] names = Enum.GetNames(typeof(NftFrameType));

            if (map.Count != names.Length)
            {
                var missing = string.Join(", ", names.Except(map.Keys.Select(e => e.ToString())));
                throw new Exception(
                    $"Missing frame prefabs for {missing}"
                );
            }

            var prefabs = map.Values.ToList();
            var distinct = prefabs.Distinct().ToList();
            if (prefabs.Count != distinct.Count)
            {
                prefabs.RemoveAll(e => distinct.Remove(e));
                var duplicated = string.Join(", ", prefabs.Select(e => e.name));
                throw new Exception($"Duplicated frame prefabs: {duplicated}");
            }

            Debug.Log("NftShapeSettings: OK");
        }

        [Serializable]
        private class Pair
        {
            public NftFrameType nftFrameType;
            public GameObject prefab = null!;
        }
    }
}
