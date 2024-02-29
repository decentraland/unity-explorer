using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering
{
    [CreateAssetMenu(menuName = "Create Avatar Randomizer Settings", fileName = "Avatar Randomizer", order = 0)]
    public class AvatarRandomizerAsset : ScriptableObject
    {
        [field: SerializeField] public bool RandomOrder { get; set; }
        [field: SerializeField] public List<RandomizedAvatar> Avatars { get; set; }
        [field: SerializeField] public List<CollectionDescription> RandomizeCollection { get; set; }
    }

    [Serializable]
    public class RandomizedAvatar
    {
        public List<string> pointers;
    }
    
    [Serializable]
    public class CollectionDescription
    {
        public string pointer;
        public string page_size;
    }
}

