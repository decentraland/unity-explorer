using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace SpringBones
{
    public readonly struct SpringBoneTransformData
    {
        public readonly float3 Position;
        public readonly quaternion Rotation;
        public readonly float3 LocalPosition;
        public readonly quaternion LocalRotation;
        public readonly float3 LocalScale;
        public readonly float4x4 LocalToWorldMatrix;

        public SpringBoneTransformData(quaternion rotation, float3 localPosition, quaternion localRotation, float3 localScale, float4x4 localToWorldMatrix)
        {
            Position = localToWorldMatrix.c3.xyz;
            Rotation = rotation;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            LocalToWorldMatrix = localToWorldMatrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SpringBoneTransformData FromTransform(Transform t)
        {
            return new SpringBoneTransformData(
                t.rotation,
                t.localPosition,
                t.localRotation,
                t.localScale,
                t.localToWorldMatrix);
        }
    }
}
