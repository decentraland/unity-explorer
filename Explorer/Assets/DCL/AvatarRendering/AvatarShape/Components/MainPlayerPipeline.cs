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

        private bool registered;
        private Transform[] bones;
        private Transform root;
        private TransformAccessArray bonesTA;
        private TransformAccessArray rootTA;
        private NativeArray<float4x4> bonesCombined;
        private NativeArray<float4x4> avatarMatrix;
        private NativeArray<bool> updateFlag;
        private bool dirty;

        public BoneMatrixCalculationJob Job;
        public bool IsRegistered => registered;

        internal MainPlayerPipeline(int bonesArrayLength)
        {
            this.bonesArrayLength = bonesArrayLength;

            bonesCombined = new NativeArray<float4x4>(bonesArrayLength, Allocator.Persistent);
            avatarMatrix = new NativeArray<float4x4>(1, Allocator.Persistent);
            updateFlag = new NativeArray<bool>(1, Allocator.Persistent);
            Job = new BoneMatrixCalculationJob(bonesArrayLength, bonesArrayLength, bonesCombined);
        }

        public void Register(Transform rootTransform, Transform[] boneTransforms)
        {
            registered = true;
            bones = boneTransforms;
            root = rootTransform;
            updateFlag[0] = true;
            dirty = true;
        }

        public void Release(ref AvatarTransformMatrixComponent component)
        {
            registered = false;
            bones = null;
            root = null;
            updateFlag[0] = false;
            dirty = true;

            component.IndexInGlobalJobArray = GlobalJobArrayIndex.Unassign();
            component.IsMainPlayer = false;
        }

        public void ScheduleAndComplete(Transform dummyTransform)
        {
            if (!registered)
                return;

            if (dirty)
                RebuildTAAs(dummyTransform);

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

        private void RebuildTAAs(Transform dummyTransform)
        {
            if (bonesTA.isCreated) bonesTA.Dispose();
            if (rootTA.isCreated) rootTA.Dispose();

            var boneArray = new Transform[bonesArrayLength];

            for (int i = 0; i < bonesArrayLength; i++)
                boneArray[i] = bones != null && bones[i] != null ? bones[i] : dummyTransform;

            bonesTA = new TransformAccessArray(boneArray);
            rootTA = new TransformAccessArray(new[] { root != null ? root : dummyTransform });
            dirty = false;
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
