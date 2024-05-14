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
        private NativeArray<int> sqrDistanceBuckets;

        private IReadOnlyList<int> partitionSettingsSqrDistanceBuckets;
        private IPartitionSettings partitionSettings;
        public int currentPartitionIndex { get; private set; }

        private int deployedSceneLimit;


        public void Initialize(int deployedSceneLimit, IReadOnlyList<int> partitionSettingsSqrDistanceBuckets, IPartitionSettings partitionSettings)
        {
            this.deployedSceneLimit = deployedSceneLimit;
            this.partitionSettingsSqrDistanceBuckets = partitionSettingsSqrDistanceBuckets;
            this.partitionSettings = partitionSettings;
            Restart();
        }

        public void Clear()
        {
            partitions.Dispose();
            Restart();
        }

        public void Dispose()
        {
            partitions.Dispose();
            sqrDistanceBuckets.Dispose();
        }

        public void SetPartitionData(ref ScenesPartitioningUtils.PartitionData partitionData)
        {
            partitions[currentPartitionIndex] = partitionData;
            currentPartitionIndex++;
        }

        private void Restart()
        {
            currentPartitionIndex = 0;
            partitions  = new NativeArray<ScenesPartitioningUtils.PartitionData>(deployedSceneLimit, Allocator.Persistent);
            // TODO: This might change with quality settings, consider updating them
            sqrDistanceBuckets = new NativeArray<int>(partitionSettingsSqrDistanceBuckets.Count, Allocator.Persistent);
            for (int i = 0; i < partitionSettingsSqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];
            partitionJob = new ScenesPartitioningUtils.ScenePartitionParallelJob(ref partitions)
            {
                SqrDistanceBuckets = sqrDistanceBuckets
            };
        }


        public JobHandle ScheduleJob(IReadOnlyCameraSamplingData readOnlyCameraSamplingData, UnsafeList<ScenesPartitioningUtils.ParcelCornersData> parcelCorners)
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
    }
}