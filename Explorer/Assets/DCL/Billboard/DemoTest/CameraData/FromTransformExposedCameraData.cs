using DCL.CharacterCamera;
using System;
using UnityEngine;

namespace ECS.Unity.Billboard.DemoTest.CameraData
{
    public class FromTransformExposedCameraData : IExposedCameraData
    {
        private readonly Transform t;

        public FromTransformExposedCameraData() : this((Camera.main ? Camera.main : throw new NullReferenceException("Camera not found"))!) { }

        public FromTransformExposedCameraData(Camera camera) : this(camera.transform, DCL.ECSComponents.CameraType.CtCinematic, false) { }

        public FromTransformExposedCameraData(Transform t, DCL.ECSComponents.CameraType cameraType, bool pointerIsLocked)
        {
            this.t = t;
            CameraType = cameraType;
            PointerIsLocked = pointerIsLocked;
        }

        public Vector3 WorldPosition => t.position;
        public Quaternion WorldRotation => t.rotation;
        public DCL.ECSComponents.CameraType CameraType { get; }
        public bool PointerIsLocked { get; }
    }
}
