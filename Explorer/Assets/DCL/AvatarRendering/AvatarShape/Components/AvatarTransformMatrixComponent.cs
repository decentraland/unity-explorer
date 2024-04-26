using DCL.AvatarRendering.AvatarShape.ComputeShader;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    public struct AvatarTransformMatrixComponent : IDisposable
    {
        private TransformAccessArray Bones;
        private BoneMatrixCalculationJob Job;
        private JobHandle Handle;

        public void ScheduleBoneMatrixCalculation(Matrix4x4 avatarWorldToLocalMatrix)
        {
            if (!Handle.IsCompleted)
                return;
            
            Job.AvatarTransform = avatarWorldToLocalMatrix;
            Handle = Job.Schedule(Bones);
        }

        public NativeArray<float4x4> CompleteBoneMatrixCalculations()
        {
            Handle.Complete();
            return Job.BonesMatricesResult;
        }

        public void Dispose()
        {
            Handle.Complete();
            Job.BonesMatricesResult.Dispose();
            Bones.Dispose();
        }

        public static AvatarTransformMatrixComponent Create(Transform avatarBaseTransform, Transform[] bones) =>
            new ()
            {
                Bones = new TransformAccessArray(bones),
                Job = new BoneMatrixCalculationJob
                {
                    BonesMatricesResult = new NativeArray<float4x4>(bones.Length, Allocator.Persistent),
                    AvatarTransform = avatarBaseTransform.worldToLocalMatrix,
                },
            };
    }
}
