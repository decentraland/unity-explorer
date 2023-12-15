using System.Collections.Generic;
using UnityEngine;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Reference to camera data that is exposed to JavaScript scene with
    ///     all Proto values prepared once in advance
    /// </summary>
    public interface IExposedCameraData
    {
        Vector3 WorldPosition { get; }

        Quaternion WorldRotation { get; }

        CameraType CameraType { get; }

        bool PointerIsLocked { get; }

        public class Fake : IExposedCameraData
        {
            public Fake(Vector3 worldPosition, Quaternion worldRotation, CameraType cameraType, bool pointerIsLocked)
            {
                WorldPosition = worldPosition;
                WorldRotation = worldRotation;
                CameraType = cameraType;
                PointerIsLocked = pointerIsLocked;
            }

            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }
            public CameraType CameraType { get; }
            public bool PointerIsLocked { get; }
        }

        public class Random : IExposedCameraData
        {
            private static readonly IReadOnlyList<CameraType> CAMERA_TYPES = new[] { CameraType.CtCinematic, CameraType.CtFirstPerson, CameraType.CtThirdPerson };

            public Random()
            {
                WorldPosition = UnityEngine.Random.insideUnitSphere;
                WorldRotation = UnityEngine.Random.rotation;
                PointerIsLocked = UnityEngine.Random.value > 0.5f;
                CameraType = CAMERA_TYPES[UnityEngine.Random.Range(0, CAMERA_TYPES.Count)];
            }

            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }
            public CameraType CameraType { get; }
            public bool PointerIsLocked { get; }
        }
    }
}
