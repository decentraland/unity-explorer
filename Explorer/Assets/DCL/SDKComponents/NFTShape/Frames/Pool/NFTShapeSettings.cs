using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Utilities.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.SDKComponents.NFTShape.Frames.Pool
{
    [CreateAssetMenu(fileName = "NftShape Settings", menuName = "SDKComponents/NftShape/Settings", order = 0)]
    public class NFTShapeSettings : ScriptableObject
    {
        [SerializeField] private List<Pair> pairs = new ();
        [SerializeField] private FrameRef defaultFrame = null!;

        public IReadOnlyDictionary<NftFrameType, FrameRef> FramePrefabs()
        {
            return pairs.ToDictionary(e => e.nftFrameType, e => e.prefab.EnsureNotNull());
        }

        public FrameRef DefaultFrame()
        {
            return defaultFrame;
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
                var duplicated = string.Join(", ", prefabs.Select(e => e.AssetGUID));
                throw new Exception($"Duplicated frame prefabs: {duplicated}");
            }

            ReportHub.Log(ReportCategory.ASSETS_PROVISION, "NftShapeSettings: OK");
        }

        [Serializable]
        private class Pair
        {
            public NftFrameType nftFrameType;
            public FrameRef prefab = null!;
        }

        [Serializable]
        public class FrameRef : ComponentReference<AbstractFrame>
        {
            public FrameRef(string guid) : base(guid) { }
        }
    }
}
