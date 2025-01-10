using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering
{
    [CreateAssetMenu(fileName = "AvatarRandomizer", menuName = "DCL/Avatar/Avatar Randomizer Settings")]
    public class AvatarRandomizerAsset : ScriptableObject
    {
        [field: SerializeField] public bool RandomOrder { get; set; }
        [field: SerializeField] public List<RandomizedAvatar> Avatars { get; set; }
    }

    [Serializable]
    public class RandomizedAvatar
    {
        public List<string> pointers;
    }
}
