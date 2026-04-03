using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DCL.SpringBones
{
    public struct BlittableTransform
    {
        public float3 Position;
        public quaternion Rotation;
        public float3 LocalPosition;
        public quaternion LocalRotation;
        public float3 LocalScale;
        public float4x4 LocalToWorldMatrix;

        public BlittableTransform(quaternion rotation, float3 localPosition, quaternion localRotation, float3 localScale, float4x4 localToWorldMatrix)
        {
            Position = localToWorldMatrix.c3.xyz;
            Rotation = rotation;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            LocalScale = localScale;
            LocalToWorldMatrix = localToWorldMatrix;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlittableTransform FromTransformAccess(TransformAccess t) =>
            new (t.rotation,
                t.localPosition,
                t.localRotation,
                t.localScale,
                t.localToWorldMatrix);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BlittableTransform FromTransform(UnityEngine.Transform t) =>
            new (t.rotation,
                t.localPosition,
                t.localRotation,
                t.localScale,
                t.localToWorldMatrix);
    }
}
