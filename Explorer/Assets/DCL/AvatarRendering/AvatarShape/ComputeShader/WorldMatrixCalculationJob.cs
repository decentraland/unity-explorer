using Unity.Collections;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public struct WorldMatrixCalculationJob : IJobParallelForTransform
    {
        [WriteOnly]
        [NativeDisableParallelForRestriction]
        private NativeArray<Matrix4x4> bonesCombined;
        private readonly int offset;

        public WorldMatrixCalculationJob(NativeArray<Matrix4x4> bonesCombined, int offset)
        {
            this.bonesCombined = bonesCombined;
            this.offset = offset;
        }

        public void Execute(int index, TransformAccess transform)
        {
            bonesCombined[offset + index] = transform.localToWorldMatrix;
        }
    }
}
