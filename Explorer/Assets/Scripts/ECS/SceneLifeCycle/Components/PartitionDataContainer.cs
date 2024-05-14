using System;
using System.Collections.Generic;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace ECS.SceneLifeCycle.Components
{
    public class PartitionDataContainer : IDisposable
    {
        public NativeArray<ScenesPartitioningUtils.PartitionData> partitions;
        public ScenesPartitioningUtils.ScenePartitionParallelJob partitionJob;
        public int currentPartitionIndex { get; private set; }

        private NativeArray<int> sqrDistanceBuckets;

        // These lists are static because of a compile issue when passing the references to the Query as [Data], code-gen wont find Unity.Collections
        private static UnsafeList<ScenesPartitioningUtils.ParcelCornersData> parcelCorners;
        private int deployedSceneLimit;

        public void Initialize(int deployedSceneLimit, IReadOnlyList<int> partitionSettingsSqrDistanceBuckets, IPartitionSettings partitionSettings)
        {
            this.deployedSceneLimit = deployedSceneLimit;
            // TODO: This might change with quality settings, consider updating them
            sqrDistanceBuckets = new NativeArray<int>(partitionSettingsSqrDistanceBuckets.Count, Allocator.Persistent);
            for (int i = 0; i < partitionSettingsSqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];
            Restart();
        }

        public void Clear()
        {
            partitions.Dispose();
            parcelCorners.Dispose();
            Restart();
        }

        public void Dispose()
        {
            partitions.Dispose();
            sqrDistanceBuckets.Dispose();
            // not sure if the parcelCorners.Dispose() will dispose its children as well so we explicitly do so here
            foreach (var cornersData in parcelCorners)
                cornersData.Dispose();

            parcelCorners.Dispose();
        }

        public void SetPartitionData(ref ScenesPartitioningUtils.PartitionData partitionData)
        {
            partitions[currentPartitionIndex] = partitionData;
            currentPartitionIndex++;
        }

        private void Restart()
        {
            currentPartitionIndex = 0;
            // Hard limit of the real scenes that can exist
            parcelCorners = new UnsafeList<ScenesPartitioningUtils.ParcelCornersData>(deployedSceneLimit, Allocator.Persistent);
            partitions  = new NativeArray<ScenesPartitioningUtils.PartitionData>(deployedSceneLimit, Allocator.Persistent);
            partitionJob = new ScenesPartitioningUtils.ScenePartitionParallelJob(ref partitions)
            {
                SqrDistanceBuckets = sqrDistanceBuckets
            };
        }


        public JobHandle ScheduleJob(IReadOnlyCameraSamplingData readOnlyCameraSamplingData)
        {
            partitionJob.CameraForward = readOnlyCameraSamplingData.Forward;
            partitionJob.CameraPosition = readOnlyCameraSamplingData.Position;
            partitionJob.ParcelCorners = parcelCorners;
            return partitionJob.Schedule(currentPartitionIndex, 8);
        }

        public void SetPartitionComponentData(int internalJobIndex, ref PartitionComponent partitionComponent)
        {
            var partition = partitions[internalJobIndex];
            partitionComponent.IsDirty = partition.IsDirty;
            partitionComponent.IsBehind = partition.IsBehind;
            partitionComponent.Bucket = partition.Bucket;
            partitionComponent.RawSqrDistance = partition.RawSqrDistance;
        }

        public void AddCorners(ScenesPartitioningUtils.ParcelCornersData parcelCornersData)
        {
            parcelCorners.Add(parcelCornersData);
        }
    }
}