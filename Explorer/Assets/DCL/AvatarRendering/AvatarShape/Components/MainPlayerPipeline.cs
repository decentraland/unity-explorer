using System;
using DCL.AvatarRendering.AvatarShape.ComputeShader;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     Dedicated pipeline for the main player avatar. Scheduled and completed immediately
    ///     each frame so its TransformAccessArray locks are released before InterpolateCharacterSystem.
    /// </summary>
    internal class MainPlayerPipeline : IDisposable
    {
        private readonly int bonesArrayLength;
        private readonly Transform[] boneArray;

        private bool registered;
        private TransformAccessArray bonesTA;
        private TransformAccessArray rootTA;
        private NativeArray<float4x4> bonesCombined;
        private NativeArray<float4x4> avatarMatrix;
        private NativeArray<bool> updateFlag;

        // Number of matrices the calculation job must produce for the main player, refreshed every frame
        // from the authoritative AvatarCustomSkinningComponent.BoneCount. The main player is never
        // released/re-registered on re-equip (its base skeleton is stable), so this per-frame refresh is
        // what keeps the count correct when new wearables add/remove spring bones. Seeded to
        // bonesArrayLength (full stride) so the very first frame degrades to original behaviour.
        private NativeArray<int> perAvatarBoneCount;

        public BoneMatrixCalculationJob Job;

        internal MainPlayerPipeline(int bonesArrayLength)
        {
            this.bonesArrayLength = bonesArrayLength;
            boneArray = new Transform[bonesArrayLength];

            bonesCombined = new NativeArray<float4x4>(bonesArrayLength, Allocator.Persistent);
            avatarMatrix = new NativeArray<float4x4>(1, Allocator.Persistent);
            updateFlag = new NativeArray<bool>(1, Allocator.Persistent);
            perAvatarBoneCount = new NativeArray<int>(1, Allocator.Persistent) { [0] = bonesArrayLength };
            Job = new BoneMatrixCalculationJob(bonesArrayLength, bonesArrayLength, bonesCombined);
        }

        /// <summary>
        ///     Refreshes the matrix count for the main player from the authoritative
        ///     AvatarCustomSkinningComponent.BoneCount. Called every frame before ScheduleAndComplete.
        /// </summary>
        public void SetBoneCount(int boneCount)
        {
            perAvatarBoneCount[0] = boneCount;
        }

        public void Register(Transform rootTransform, BoneArray bones, Transform dummyTransform)
        {
            updateFlag[0] = true;

            // bonesArrayLength is the per-avatar slot capacity (MAX_BONE_COUNT = 256).
            // bones.Count is BASE_BONE_COUNT (62) plus appended spring bones, so it is
            // typically smaller than bonesArrayLength — the trailing slots get padded with
            // dummyTransform. Mathf.Min guards against a hypothetical out-of-range BoneArray.
            int actualCount = Mathf.Min(bones.Count, bonesArrayLength);

            for (int i = 0; i < actualCount; i++)
                boneArray[i] = bones[i];

            for (int i = actualCount; i < bonesArrayLength; i++)
                boneArray[i] = dummyTransform;

            if (bonesTA.isCreated) bonesTA.Dispose();
            if (rootTA.isCreated) rootTA.Dispose();

            bonesTA = new TransformAccessArray(boneArray);
            rootTA = new TransformAccessArray(new[] { rootTransform });
            registered = true;
        }

        public void ScheduleAndComplete()
        {
            if (!registered)
                return;

            var boneGather = new BoneGatherJob { BonesCombined = bonesCombined };
            var boneGatherHandle = boneGather.Schedule(bonesTA);

            var rootGather = new AvatarRootGatherJob { MatrixFromAllAvatars = avatarMatrix };
            var rootGatherHandle = rootGather.Schedule(rootTA);

            var gatherHandle = JobHandle.CombineDependencies(boneGatherHandle, rootGatherHandle);

            Job.AvatarTransform = avatarMatrix;
            Job.UpdateAvatar = updateFlag;
            Job.PerAvatarBoneCount = perAvatarBoneCount;
            var calcHandle = Job.Schedule(1, 1, gatherHandle);
            calcHandle.Complete(); // Fast — 1 avatar, 62 bones. Unlocks main player transforms.
        }

        public void Dispose()
        {
            bonesCombined.Dispose();
            avatarMatrix.Dispose();
            updateFlag.Dispose();
            perAvatarBoneCount.Dispose();
            Job.Dispose();

            if (bonesTA.isCreated) bonesTA.Dispose();
            if (rootTA.isCreated) rootTA.Dispose();
        }
    }
}
