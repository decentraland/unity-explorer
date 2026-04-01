using System.Collections.Generic;
using UniGLTF.SpringBoneJobs.InputPorts;
using UnityEngine;

namespace DCL.SpringBones
{
    public struct SpringBoneRegistrationComponent
    {
        public FastSpringBoneBuffer Buffer;
        public int AvatarVersion;
        public List<(Transform wearableParent, Transform skeletonBone)> SyncPairs;
    }
}
