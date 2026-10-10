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
    ///     Reads each avatar root's matrix from worker threads: the inverse into matrixFromAllAvatars for the
    ///     bone calculation, and the forward matrix into localToWorldFromAllAvatars so the bounds transform can
    ///     run in the job instead of touching Transform again on the main thread.
    ///     The TAA has one entry per slot (including dummy entries for released slots),
    ///     so transform index maps directly to both arrays.
    /// </summary>
    [BurstCompile]
    public struct AvatarRootGatherJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> MatrixFromAllAvatars;

        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> LocalToWorldFromAllAvatars;

        public void Execute(int index, TransformAccess transform)
        {
            var localToWorld = (float4x4)transform.localToWorldMatrix;

            LocalToWorldFromAllAvatars[index] = localToWorld;
            MatrixFromAllAvatars[index] = math.inverse(localToWorld);
        }
    }
}
