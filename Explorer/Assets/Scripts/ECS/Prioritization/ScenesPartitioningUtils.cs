using CrdtEcsBridge.Components.Special;
using ECS.Prioritization.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Prioritization
{
    public static class ScenesPartitioningUtils
    {
        private const float PARCEL_SIZE_SQR = ParcelMathHelper.PARCEL_SIZE * ParcelMathHelper.PARCEL_SIZE;

        public static void CheckCameraTransformChanged(PartitionDiscreteDataBase partitionDiscreteData, in CameraComponent cameraComponent,
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
        }

        /// <summary>
        ///     Partitioning is performed accordingly to the closest scene parcel to the camera.
        /// </summary>
        public static void Partition(IPartitionSettings partitionSettings, IReadOnlyList<Vector2Int> sceneParcels, in IReadOnlyCameraSamplingData readOnlyCameraData, ref PartitionComponent partitionComponent)
        {
            byte bucket = partitionComponent.Bucket;
            bool isBehind = partitionComponent.IsBehind;

            // Find the closest scene parcel
            // The Y component can be safely ignored as all plots are allocated on one plane

            int minSqrMagnitude = int.MaxValue;
            Vector2Int vectorToCamera = Vector2Int.zero;

            for (var i = 0; i < sceneParcels.Count; i++)
            {
                Vector2Int vct = sceneParcels[i] - readOnlyCameraData.Parcel;
                int sqr = vct.sqrMagnitude;

                if (sqr < minSqrMagnitude)
                {
                    minSqrMagnitude = sqr;
                    vectorToCamera = vct;
                }
            }

            // translate parcel back to coordinates
            float sqrDistance = minSqrMagnitude * PARCEL_SIZE_SQR;
            Vector3 vectorToCameraCoords = new Vector3(vectorToCamera.x, 0, vectorToCamera.y) * ParcelMathHelper.PARCEL_SIZE;

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
            partitionComponent.IsBehind = vectorToCamera != Vector2Int.zero && Vector3.Dot(readOnlyCameraData.Forward, vectorToCameraCoords) < 0;
            partitionComponent.IsDirty = partitionComponent.Bucket != bucket || partitionComponent.IsBehind != isBehind;
        }
    }
}
