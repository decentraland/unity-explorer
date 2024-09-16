using Arch.Core;
using Cinemachine;
using DCL.CharacterCamera;
using DCL.Utilities;
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
        public CanBeDirty<CameraType> CameraType { get; }
        public CanBeDirty<bool> PointerIsLocked { get; }
        public ObjectProxy<Entity> CameraEntityProxy { get; } = new ();
        public CinemachineBrain? CinemachineBrain { get; set; }
        public CameraMode CameraMode { get; set; }

        public FromTransformExposedCameraData() : this((Camera.main ? Camera.main : throw new NullReferenceException("Camera not found"))!) { }

        public FromTransformExposedCameraData(Camera camera) : this(camera.transform, ECSComponents.CameraType.CtCinematic, false) { }

        public FromTransformExposedCameraData(Transform t, CameraType cameraType, bool pointerIsLocked)
        {
            this.t = t;
            CameraType = CanBeDirty.FromEnum(cameraType);
            PointerIsLocked = new CanBeDirty<bool>(pointerIsLocked);
        }
    }
}
