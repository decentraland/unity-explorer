using UnityEngine;

namespace DCL.AvatarRendering.Loading.Assets
{
    public readonly struct SpringBoneData
    {
        public readonly Transform ManagedTransform;
        public readonly bool IsRoot;
        public readonly int AvatarSkeletonParentBoneIndex;
        public readonly float Stiffness;
        public readonly float Drag;
        public readonly Vector3 GravityDir;
        public readonly float GravityPower;
        public readonly float HitRadius;
        public readonly Quaternion InitialLocalRotation;

        public SpringBoneData(Transform managedTransform,
            bool isRoot,
            int avatarSkeletonParentBoneIndex,
            float stiffness,
            float drag,
            Vector3 gravityDir,
            float gravityPower,
            float hitRadius,
            Quaternion initialLocalRotation)
        {
            ManagedTransform = managedTransform;
            IsRoot = isRoot;
            AvatarSkeletonParentBoneIndex = avatarSkeletonParentBoneIndex;
            Stiffness = stiffness;
            Drag = drag;
            GravityDir = gravityDir;
            GravityPower = gravityPower;
            HitRadius = hitRadius;
            InitialLocalRotation = initialLocalRotation;
        }
    }
}
