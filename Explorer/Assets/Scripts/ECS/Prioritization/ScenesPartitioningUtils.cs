using CrdtEcsBridge.Components.Special;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Prioritization
{
    public static class ScenesPartitioningUtils
    {
        public static bool CheckCameraTransformChanged(PartitionDiscreteDataBase partitionDiscreteData, in CameraComponent cameraComponent,
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
                partitionDiscreteData.Parcel = ParcelMathHelper.FloorToParcel(position);
                partitionDiscreteData.IsDirty = true;
            }
            else partitionDiscreteData.IsDirty = false;

            return partitionDiscreteData.IsDirty;
        }

        /// <summary>
        ///     Partitioning is performed accordingly to the closest scene parcel to the camera.
        /// </summary>
        public static void Partition(IPartitionSettings partitionSettings, IReadOnlyList<ParcelMathHelper.ParcelCorners> parcelsCorners, IReadOnlyCameraSamplingData readOnlyCameraData, PartitionComponent partitionComponent)
        {
            byte bucket = partitionComponent.Bucket;
            bool isBehind = partitionComponent.IsBehind;

            // Find the closest scene parcel
            // The Y component can be safely ignored as all plots are allocated on one plane

            // Is Behind must be calculated for each parcel the scene contains
            partitionComponent.IsBehind = true;

            float minSqrMagnitude = float.MaxValue;

            for (var i = 0; i < parcelsCorners.Count; i++)
            {
                void ProcessCorners(Vector3 corner)
                {
                    Vector3 vectorToCamera = corner - readOnlyCameraData.Position;
                    vectorToCamera.y = 0; // ignore Y
                    float sqr = vectorToCamera.sqrMagnitude;

                    if (sqr < minSqrMagnitude)
                        minSqrMagnitude = sqr;

                    // partition is not behind if at least one corner is not behind
                    if (partitionComponent.IsBehind)
                        partitionComponent.IsBehind = Vector3.Dot(readOnlyCameraData.Forward, vectorToCamera) < 0;
                }

                ParcelMathHelper.ParcelCorners corners = parcelsCorners[i];
                ProcessCorners(corners.minXZ);
                ProcessCorners(corners.minXmaxZ);
                ProcessCorners(corners.maxXminZ);
                ProcessCorners(corners.maxXZ);
            }

            // translate parcel back to coordinates
            float sqrDistance = minSqrMagnitude;

            // Find the bucket
            byte bucketIndex;

            for (bucketIndex = 0; bucketIndex < partitionSettings.SqrDistanceBuckets.Count; bucketIndex++)
            {
                if (sqrDistance < partitionSettings.SqrDistanceBuckets[bucketIndex])
                    break;
            }

            partitionComponent.Bucket = bucketIndex;

            // Is behind is a dot product
            // mind that taking cosines is not cheap
            // the same scene is counted as InFront
            partitionComponent.IsDirty = partitionComponent.Bucket != bucket || partitionComponent.IsBehind != isBehind;
        }
    }
}
