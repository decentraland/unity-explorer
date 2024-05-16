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
        private TransformAccessArray bones;
        private BoneMatrixCalculationJob job;
        private JobHandle handle;

        internal bool disposed { get; private set; }

        internal bool completed { get; private set; }

        public void ScheduleBoneMatrixCalculation(Matrix4x4 avatarWorldToLocalMatrix)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(AvatarTransformMatrixComponent), $"{nameof(ScheduleBoneMatrixCalculation)} called on the disposed component");

            if (!handle.IsCompleted)
                return;

            job.AvatarTransform = avatarWorldToLocalMatrix;
            handle = job.Schedule(bones);
            completed = false;
        }

        public NativeArray<float4x4> CompleteBoneMatrixCalculations()
        {
            handle.Complete();
            completed = true;
            return job.BonesMatricesResult;
        }

        public void Dispose()
        {
            handle.Complete();
            job.BonesMatricesResult.Dispose();
            bones.Dispose();

            disposed = true;
            completed = true;
        }

        public static AvatarTransformMatrixComponent Create(Transform avatarBaseTransform, Transform[] bones) =>
            new ()
            {
                bones = new TransformAccessArray(bones),
                job = new BoneMatrixCalculationJob
                {
                    BonesMatricesResult = new NativeArray<float4x4>(bones.Length, Allocator.Persistent),
                    AvatarTransform = avatarBaseTransform.worldToLocalMatrix,
                },
            };
    }
}
