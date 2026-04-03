using Unity.Mathematics;

namespace DCL.SpringBones
{
    public struct BlittableJointConfig
    {
        public float Stiffness;
        public float Drag;
        public float3 GravityDir;
        public float GravityPower;
        public float3 BoneAxis;
        public float Length;
        public quaternion LocalRotation;
    }
}
