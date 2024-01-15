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

        [Serializable]
        private class Pair
        {
            public NftFrameType nftFrameType;
            public GameObject prefab = null!;
        }
    }
}
