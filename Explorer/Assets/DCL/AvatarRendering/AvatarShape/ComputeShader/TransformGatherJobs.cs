using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    /// <summary>
    ///     Reads each bone's localToWorldMatrix from worker threads into the flat bonesCombined array.
    ///     The TAA is laid out as: [bone0_slot0 … bone61_slot0 | bone0_slot1 … bone61_slot1 | …]
    ///     so transform index maps directly to bonesCombined index with no remapping.
    /// </summary>
    [BurstCompile]
    public struct BoneGatherJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> BonesCombined;

        public void Execute(int index, TransformAccess transform)
        {
            BonesCombined[index] = transform.localToWorldMatrix;
        }
    }

    /// <summary>
    ///     Reads each avatar root's worldToLocalMatrix from worker threads into matrixFromAllAvatars.
    ///     The TAA has one entry per slot (including dummy entries for released slots),
    ///     so transform index maps directly to matrixFromAllAvatars index.
    /// </summary>
    [BurstCompile]
    public struct AvatarRootGatherJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> MatrixFromAllAvatars;

        public void Execute(int index, TransformAccess transform)
        {
            MatrixFromAllAvatars[index] = math.inverse((float4x4)transform.localToWorldMatrix);
        }
    }
}