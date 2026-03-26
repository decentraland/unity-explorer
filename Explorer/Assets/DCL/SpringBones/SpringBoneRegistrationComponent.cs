using UniGLTF.SpringBoneJobs.InputPorts;
using UnityEngine;

namespace DCL.SpringBones
{
    public struct SpringBoneRegistrationComponent
    {
        public FastSpringBoneBuffer Buffer;
        public int LastKnownVersion;
        public Transform[] Clones;
    }
}
