using DCL.CharacterCamera;
using System;
using UnityEngine;
using Utility;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.Billboard.Demo.CameraData
{
    public class FromTransformExposedCameraData : IExposedCameraData
    {
        private readonly Transform t;

        public CanBeDirty<Vector3> WorldPosition => new (t.position);
        public CanBeDirty<Quaternion> WorldRotation => new (t.rotation);
        public CameraType CameraType { get; }
        public bool PointerIsLocked { get; }

        public FromTransformExposedCameraData() : this((Camera.main ? Camera.main : throw new NullReferenceException("Camera not found"))!) { }

        public FromTransformExposedCameraData(Camera camera) : this(camera.transform, CameraType.CtCinematic, false) { }

        public FromTransformExposedCameraData(Transform t, CameraType cameraType, bool pointerIsLocked)
        {
            this.t = t;
            CameraType = cameraType;
            PointerIsLocked = pointerIsLocked;
        }
    }
}
