using UnityEngine;
using Utility;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    public class ExposedCameraData : IExposedCameraData
    {
        public CanBeDirty<Vector3> WorldPosition;
        public CanBeDirty<Quaternion> WorldRotation;

        public CameraType CameraType { get; set; }
        public bool PointerIsLocked { get; set; }

        CanBeDirty<Vector3> IExposedCameraData.WorldPosition => WorldPosition;
        CanBeDirty<Quaternion> IExposedCameraData.WorldRotation => WorldRotation;
    }
}
