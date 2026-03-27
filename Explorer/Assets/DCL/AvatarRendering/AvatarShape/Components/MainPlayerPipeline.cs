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

        public BoneMatrixCalculationJob Job;

        internal MainPlayerPipeline(int bonesArrayLength)
        {
            this.bonesArrayLength = bonesArrayLength;
            boneArray = new Transform[bonesArrayLength];

            bonesCombined = new NativeArray<float4x4>(bonesArrayLength, Allocator.Persistent);
            avatarMatrix = new NativeArray<float4x4>(1, Allocator.Persistent);
            updateFlag = new NativeArray<bool>(1, Allocator.Persistent);
            Job = new BoneMatrixCalculationJob(bonesArrayLength, bonesArrayLength, bonesCombined);
        }

        public void Register(Transform rootTransform, Transform[] boneTransforms, Transform dummyTransform)
        {
            updateFlag[0] = true;

            int actualCount = Mathf.Min(boneTransforms.Length, bonesArrayLength);

            for (int i = 0; i < actualCount; i++)
                boneArray[i] = boneTransforms[i];

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
            var calcHandle = Job.Schedule(1, 1, gatherHandle);
            calcHandle.Complete(); // Fast — 1 avatar, 62 bones. Unlocks main player transforms.
        }

        public void Dispose()
        {
            bonesCombined.Dispose();
            avatarMatrix.Dispose();
            updateFlag.Dispose();
            Job.Dispose();

            if (bonesTA.isCreated) bonesTA.Dispose();
            if (rootTA.isCreated) rootTA.Dispose();
        }
    }
}
