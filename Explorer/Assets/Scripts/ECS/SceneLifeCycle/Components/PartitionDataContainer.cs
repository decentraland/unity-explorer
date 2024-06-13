using System;
using System.Collections.Generic;
using ECS.Prioritization;
using ECS.Prioritization.Components;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace ECS.SceneLifeCycle.Components
{
    public class PartitionDataContainer : IDisposable
    {
        private NativeArray<ScenesPartitioningUtils.PartitionData> partitions;
        private ScenesPartitioningUtils.ScenePartitionParallelJob partitionJob;
        private int deployedSceneLimit;

        public ref readonly NativeArray<ScenesPartitioningUtils.PartitionData> Partitions => ref partitions;
        public int CurrentPartitionIndex { get; private set; }

        // These lists are static because of a compile issue when passing the references to the Query as [Data], code-gen wont find Unity.Collections
        private UnsafeList<ScenesPartitioningUtils.ParcelCornersData> parcelCorners;

        private NativeArray<int> sqrDistanceBuckets;

        public void Dispose()
        {
            partitions.Dispose();
            sqrDistanceBuckets.Dispose();

            // not sure if the parcelCorners.Dispose() will dispose its children as well so we explicitly do so here
            foreach (ScenesPartitioningUtils.ParcelCornersData cornersData in parcelCorners)
                cornersData.Dispose();

            parcelCorners.Dispose();
        }

        public void Initialize(int deployedSceneLimit, IReadOnlyList<int> partitionSettingsSqrDistanceBuckets, IPartitionSettings partitionSettings)
        {
            this.deployedSceneLimit = deployedSceneLimit;

            // TODO: This might change with quality settings, consider updating them
            sqrDistanceBuckets = new NativeArray<int>(partitionSettingsSqrDistanceBuckets.Count, Allocator.Persistent);

            for (var i = 0; i < partitionSettingsSqrDistanceBuckets.Count; i++)
                sqrDistanceBuckets[i] = partitionSettings.SqrDistanceBuckets[i];
        }


        public void SetPartitionData(ScenesPartitioningUtils.PartitionData partitionData)
        {
            partitions[CurrentPartitionIndex] = partitionData;
            CurrentPartitionIndex++;
        }

        public void Restart()
        {
            partitions.Dispose();
            parcelCorners.Dispose();

            CurrentPartitionIndex = 0;

            // Hard limit of the real scenes that can exist
            parcelCorners = new UnsafeList<ScenesPartitioningUtils.ParcelCornersData>(deployedSceneLimit, Allocator.Persistent);
            partitions = new NativeArray<ScenesPartitioningUtils.PartitionData>(deployedSceneLimit, Allocator.Persistent);

            partitionJob = new ScenesPartitioningUtils.ScenePartitionParallelJob(partitions)
            {
                SqrDistanceBuckets = sqrDistanceBuckets,
            };
        }

        public JobHandle ScheduleJob(float2 playerPosition, IReadOnlyCameraSamplingData readOnlyCameraSamplingData, float unloadSqrDistance)
        {
            partitionJob.CameraForward = readOnlyCameraSamplingData.Forward;
            partitionJob.PlayerPosition = playerPosition;
            partitionJob.CameraPosition = readOnlyCameraSamplingData.Position;
            partitionJob.ParcelCorners = parcelCorners;
            partitionJob.UnloadingSqrDistance = unloadSqrDistance;
            return partitionJob.Schedule(CurrentPartitionIndex, 8);
        }

        public void SetPartitionComponentData(int internalJobIndex, ref PartitionComponent partitionComponent)
        {
            ScenesPartitioningUtils.PartitionData partition = Partitions[internalJobIndex];
            partitionComponent.IsDirty = partition.IsDirty;
            partitionComponent.IsBehind = partition.IsBehind;
            partitionComponent.Bucket = partition.Bucket;
            partitionComponent.RawSqrDistance = partition.RawSqrDistance;
            partitionComponent.OutOfRange = partition.OutOfRange;
            partitionComponent.IsPlayerInScene = partition.IsPlayerInScene;
        }

        public void AddCorners(ScenesPartitioningUtils.ParcelCornersData parcelCornersData)
        {
            parcelCorners.Add(parcelCornersData);
        }
    }
}
