using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    public class ExposedCameraData : IExposedCameraData
    {
        public Vector3 WorldPosition { get; set; }
        public Quaternion WorldRotation { get; set; }
        public CameraType CameraType { get; set; }
        public bool PointerIsLocked { get; set; }
    }
}
