using System.Collections.Generic;
using UnityEngine;

namespace DCL.SpringBones
{
    public struct SpringBoneRegistrationComponent
    {
        public List<int> SlotIndices;
        public int AvatarVersion;
        public List<(Transform wearableParent, Transform skeletonBone)> SyncPairs;
    }
}
