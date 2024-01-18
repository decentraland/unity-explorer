using System.Collections.Generic;
using UnityEngine;
using Utility;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Reference to camera data that is exposed to JavaScript scene with
    ///     all Proto values prepared once in advance
    /// </summary>
    public interface IExposedCameraData : IExposedTransform
    {
        CanBeDirty<Vector3> WorldPosition { get; }

        CanBeDirty<Quaternion> WorldRotation { get; }

        CameraType CameraType { get; }

        bool PointerIsLocked { get; }

        CanBeDirty<Vector3> IExposedTransform.Position => WorldPosition;
        CanBeDirty<Quaternion> IExposedTransform.Rotation => WorldRotation;

        public class Fake : IExposedCameraData
        {
            public CanBeDirty<Vector3> WorldPosition { get; }
            public CanBeDirty<Quaternion> WorldRotation { get; }
            public CameraType CameraType { get; }
            public bool PointerIsLocked { get; }

            public Fake(Vector3 worldPosition, Quaternion worldRotation, CameraType cameraType, bool pointerIsLocked)
            {
                WorldPosition = new CanBeDirty<Vector3>(worldPosition);
                WorldRotation = new CanBeDirty<Quaternion>(worldRotation);
                CameraType = cameraType;
                PointerIsLocked = pointerIsLocked;
            }
        }

        public class Random : IExposedCameraData
        {
            private static readonly IReadOnlyList<CameraType> CAMERA_TYPES = new[] { CameraType.CtCinematic, CameraType.CtFirstPerson, CameraType.CtThirdPerson };

            public CanBeDirty<Vector3> WorldPosition { get; }

            public CanBeDirty<Quaternion> WorldRotation { get; }
            public CameraType CameraType { get; }
            public bool PointerIsLocked { get; }

            public Random()
            {
                WorldPosition = new CanBeDirty<Vector3>(UnityEngine.Random.insideUnitSphere);
                WorldRotation = new CanBeDirty<Quaternion>(UnityEngine.Random.rotation);
                PointerIsLocked = UnityEngine.Random.value > 0.5f;
                CameraType = CAMERA_TYPES[UnityEngine.Random.Range(0, CAMERA_TYPES.Count)];
            }
        }
    }
}
