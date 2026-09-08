using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    [BurstCompile]
    public struct BoneMatrixCalculationJob : IJobParallelFor, IDisposable
    {
        private readonly int boneStride;

        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> bonesMatricesResult;
        [NativeDisableParallelForRestriction]
        public NativeArray<float4x4> AvatarTransform;
        [NativeDisableParallelForRestriction]
        private NativeArray<float4x4> boneWorldMatrixArray;

        [NativeDisableParallelForRestriction] public NativeArray<bool> UpdateAvatar;

        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<int> PerAvatarBoneCount;

        /// <summary>Avatar root localToWorld, gathered by <see cref="AvatarRootGatherJob" />.</summary>
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float4x4> AvatarLocalToWorld;

        /// <summary>Per-avatar bounds in avatar space, c0 is the centre and c1 the extents.</summary>
        [ReadOnly] [NativeDisableParallelForRestriction] public NativeArray<float3x2> LocalBounds;

        /// <summary>Per-avatar bounds in world space, same layout as <see cref="LocalBounds" />.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float3x2> WorldBounds;

        public NativeArray<float4x4> BonesMatricesResult => bonesMatricesResult;

        public BoneMatrixCalculationJob(int boneStride, int bonesPerAvatarLength, NativeArray<float4x4> boneWorldMatrixArray)
        {
            this.boneStride = boneStride;
            bonesMatricesResult = new NativeArray<float4x4>(bonesPerAvatarLength, Allocator.Persistent);
            AvatarTransform = default;
            UpdateAvatar = default;
            PerAvatarBoneCount = default;
            AvatarLocalToWorld = default;
            LocalBounds = default;
            WorldBounds = default;

            this.boneWorldMatrixArray = boneWorldMatrixArray;
        }

        public void Dispose()
        {
            bonesMatricesResult.Dispose();
        }

        // Each parallel task handles one avatar: the UpdateAvatar check runs once, AvatarTransform is
        // loaded once, and the inner bone loop is a tight sequential range that Burst can auto-vectorize.
        public void Execute(int avatarIdx)
        {
            if (!UpdateAvatar[avatarIdx])
                return;

            float4x4 avatarMatrix = AvatarTransform[avatarIdx];
            int offset = avatarIdx * boneStride;
            int count = math.min(PerAvatarBoneCount[avatarIdx], boneStride);

            for (int b = 0; b < count; b++)
                bonesMatricesResult[offset + b] = math.mul(avatarMatrix, boneWorldMatrixArray[offset + b]);

            // Re-axis-align the avatar-space bounds through the root matrix. The absolute-valued rotation
            // maps each local extent onto the world axes, which is the same result Bounds gives on the main
            // thread; doing it here keeps the culling test off Transform entirely.
            float4x4 localToWorld = AvatarLocalToWorld[avatarIdx];
            float3x2 local = LocalBounds[avatarIdx];

            var absRotation = new float3x3(
                math.abs(localToWorld.c0.xyz),
                math.abs(localToWorld.c1.xyz),
                math.abs(localToWorld.c2.xyz));

            WorldBounds[avatarIdx] = new float3x2(
                math.transform(localToWorld, local.c0),
                math.mul(absRotation, local.c1));
        }
    }
}
