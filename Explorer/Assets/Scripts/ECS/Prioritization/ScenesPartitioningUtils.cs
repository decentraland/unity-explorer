using DCL.CharacterCamera;
using ECS.Prioritization.Components;
using UnityEngine;
using static Utility.ParcelMathHelper;

namespace ECS.Prioritization
{
    public static class ScenesPartitioningUtils
    {
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
    }
}
