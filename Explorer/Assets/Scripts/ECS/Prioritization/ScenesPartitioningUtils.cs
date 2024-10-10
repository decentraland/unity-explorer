﻿using DCL.CharacterCamera;
using ECS.Prioritization.Components;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Utility.ParcelMathHelper;

namespace ECS.Prioritization
{
    public static class ScenesPartitioningUtils
    {
        public struct PartitionData
        {
            public bool IsDirty;
            public byte Bucket;
            public bool IsBehind;
            public bool OutOfRange;
            public float RawSqrDistance;
            public float TimeOutOfBucket;
        }

        public static bool TryUpdateCameraTransformOnChanged(PartitionDiscreteDataBase partitionDiscreteData, in CameraComponent cameraComponent,
            float sqrPositionTolerance, float angleTolerance)
        {
            Transform camTransform = cameraComponent.Camera.transform;

            Vector3 position = camTransform.localPosition;
            Quaternion rotation = camTransform.localRotation;

            if (Vector3.SqrMagnitude(position - partitionDiscreteData.Position) > sqrPositionTolerance
                || Quaternion.Angle(rotation, partitionDiscreteData.Rotation) > angleTolerance)
            {
                partitionDiscreteData.Position = position;
                partitionDiscreteData.Rotation = rotation;
                partitionDiscreteData.Forward = camTransform.forward;
                partitionDiscreteData.Parcel = position.ToParcel();
                partitionDiscreteData.IsDirty = true;
            }
            else partitionDiscreteData.IsDirty = false;

            return partitionDiscreteData.IsDirty;
        }

        public struct ParcelCornersData : IDisposable
        {
            public NativeArray<ParcelCorners> Corners;

            public ParcelCornersData(in NativeArray<ParcelCorners> corners)
            {
                Corners = corners;
            }

            public void Dispose()
            {
                Corners.Dispose();
            }
        }

        [BurstCompile]
        public struct ScenePartitionParallelJob : IJobParallelFor
        {
            public float3 CameraPosition;
            public float3 CameraForward;
            public float UnloadingSqrDistance;
            public int LODBucket;
            public float UnloadingTime;
            public float DeltaTime;
            [ReadOnly] public NativeArray<int> SqrDistanceBuckets;
            [ReadOnly] public UnsafeList<ParcelCornersData> ParcelCorners;
            private NativeArray<PartitionData> partitions;

            public ScenePartitionParallelJob(NativeArray<PartitionData> partitions)
            {
                this.partitions = partitions;
                ParcelCorners = default(UnsafeList<ParcelCornersData>);
                CameraPosition = default;
                CameraForward = default;
                SqrDistanceBuckets = default(NativeArray<int>);
                UnloadingSqrDistance = default;
                UnloadingTime = default;
                DeltaTime = default;
                LODBucket = default;
            }

            public void Execute(int index)
            {
                ParcelCornersData corners = ParcelCorners[index];
                PartitionData partition = partitions[index];
                byte bucket = partition.Bucket;
                bool isBehind = partition.IsBehind;

                // Find the closest scene parcel
                // The Y component can be safely ignored as all plots are allocated on one plane

                // Is Behind must be calculated for each parcel the scene contains
                partition.IsBehind = true;

                float minSqrMagnitude = float.MaxValue;

                for (var i = 0; i < corners.Corners.Length; i++)
                {
                    void ProcessCorners(float3 corner, ref PartitionData partitionData, ref float3 position, ref float3 forward)
                    {
                        Vector3 vectorToCamera = corner - position;
                        vectorToCamera.y = 0; // ignore Y
                        float sqr = vectorToCamera.sqrMagnitude;

                        if (sqr < minSqrMagnitude)
                            minSqrMagnitude = sqr;

                        // partition is not behind if at least one corner is not behind
                        if (partitionData.IsBehind)
                            partitionData.IsBehind = Vector3.Dot(forward, vectorToCamera) < 0;
                    }

                    ParcelCorners corners1 = corners.Corners[i];
                    ProcessCorners(corners1.minXZ, ref partition, ref CameraPosition, ref CameraForward);
                    ProcessCorners(corners1.minXmaxZ, ref partition, ref CameraPosition, ref CameraForward);
                    ProcessCorners(corners1.maxXminZ, ref partition, ref CameraPosition, ref CameraForward);
                    ProcessCorners(corners1.maxXZ, ref partition, ref CameraPosition, ref CameraForward);
                }

                // Find the bucket
                byte newBucketIndex;

                for (newBucketIndex = 0; newBucketIndex < SqrDistanceBuckets.Length; newBucketIndex++)
                {
                    if (minSqrMagnitude < SqrDistanceBuckets[newBucketIndex])
                        break;
                }


                //We only change bucket from a lower bucket up to LODBucket after an UnloadingTime has passed.
                //This allows for some leniency so scenes are not unloaded immediately as soon as they are in the right bucket.
                //But ONLY when going from a lower bucket to the LODBucket. In any other case the scenes will be re-bucketed immediately.
                if (partition.Bucket == newBucketIndex)
                    partition.TimeOutOfBucket = 0;
                else if (newBucketIndex == LODBucket && newBucketIndex > partition.Bucket && partition.TimeOutOfBucket < UnloadingTime)
                {
                    partition.TimeOutOfBucket += DeltaTime;
                    //With this we make sure the partition is re-bucketed to the highest possible bucket before it becomes a LOD
                    //i.e. if it was 0 and needs to go to 2, we switch it to 1 until the time out of bucket passes.
                    partition.Bucket = newBucketIndex--;
                }
                else
                    partition.Bucket = newBucketIndex;


                // Is behind is a dot product
                // mind that taking cosines is not cheap
                // the same scene is counted as InFront
                // If the bucket exceeds the maximum bucket array, we need to mark partition as dirty since we are out of range
                partition.IsDirty = partition.Bucket != bucket || partition.IsBehind != isBehind || newBucketIndex == SqrDistanceBuckets.Length || partition.RawSqrDistance == -1;

                partition.OutOfRange = minSqrMagnitude > UnloadingSqrDistance;

                if (partition.IsDirty)
                    partition.RawSqrDistance = minSqrMagnitude;

                partitions[index] = partition;
            }
        }
    }
}
