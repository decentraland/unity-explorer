using Arch.Core;
using Cinemachine;
using DCL.Utilities;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     Running total of raw pointer delta. Double-backed so the accumulation does not drift
    ///     from float precision loss over long sessions. Consumers diff two snapshots to recover
    ///     the motion of the interval; nobody resets the total.
    /// </summary>
    public readonly struct CumulativePointerDelta
    {
        public readonly double X;
        public readonly double Y;

        public CumulativePointerDelta(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static CumulativePointerDelta operator +(CumulativePointerDelta total, Vector2 frameDelta) =>
            new (total.X + frameDelta.x, total.Y + frameDelta.y);

        /// <summary>
        ///     Difference between two snapshots as a Vector2. The interval is short enough for float precision.
        /// </summary>
        public static Vector2 operator -(CumulativePointerDelta total, CumulativePointerDelta snapshot) =>
            new ((float)(total.X - snapshot.X), (float)(total.Y - snapshot.Y));
    }

    /// <summary>
    ///     Reference to camera data that is exposed to JavaScript scene with
    ///     all Proto values prepared once in advance
    /// </summary>
    public interface IExposedCameraData : IExposedTransform
    {
        CanBeDirty<Vector3> WorldPosition { get; }

        CanBeDirty<Quaternion> WorldRotation { get; }

        CanBeDirty<CameraType> CameraType { get; }

        CanBeDirty<bool> PointerIsLocked { get; }

        /// <summary>
        ///     Raw pointer delta accumulated every render frame in the global world, regardless of lock state.
        /// </summary>
        CumulativePointerDelta AccumulatedPointerDelta { get; }

        ObjectProxy<Entity> CameraEntityProxy { get; }

        CanBeDirty<Vector3> IExposedTransform.Position => WorldPosition;
        CanBeDirty<Quaternion> IExposedTransform.Rotation => WorldRotation;
        CinemachineBrain? CinemachineBrain { get; set; }
        CameraMode CameraMode { get; set; }

        public class Fake : IExposedCameraData
        {
            public CanBeDirty<Vector3> WorldPosition { get; }
            public CanBeDirty<Quaternion> WorldRotation { get; }
            public CanBeDirty<CameraType> CameraType { get; }
            public CanBeDirty<bool> PointerIsLocked { get; }
            public CumulativePointerDelta AccumulatedPointerDelta { get; }

            public ObjectProxy<Entity> CameraEntityProxy { get; } = new ();
            public CinemachineBrain? CinemachineBrain { get; set; }
            public CameraMode CameraMode { get; set; }

            public Fake(Vector3 worldPosition, Quaternion worldRotation, CameraType cameraType, bool pointerIsLocked)
            {
                WorldPosition = new CanBeDirty<Vector3>(worldPosition);
                WorldRotation = new CanBeDirty<Quaternion>(worldRotation);
                CameraType = CanBeDirty.FromEnum(cameraType);
                PointerIsLocked = new CanBeDirty<bool>(pointerIsLocked);
            }
        }

        public class Random : IExposedCameraData
        {
            private static readonly IReadOnlyList<CameraType> CAMERA_TYPES = new[]
            {
                ECSComponents.CameraType.CtCinematic,
                ECSComponents.CameraType.CtFirstPerson,
                ECSComponents.CameraType.CtThirdPerson,
            };

            public CanBeDirty<Vector3> WorldPosition { get; }

            public CanBeDirty<Quaternion> WorldRotation { get; }
            public CanBeDirty<CameraType> CameraType { get; }
            public CanBeDirty<bool> PointerIsLocked { get; }
            public CumulativePointerDelta AccumulatedPointerDelta { get; }
            public ObjectProxy<Entity> CameraEntityProxy { get; } = new ();
            public CinemachineBrain? CinemachineBrain { get; set; }
            public CameraMode CameraMode { get; set; }

            public Random()
            {
                WorldPosition = new CanBeDirty<Vector3>(UnityEngine.Random.insideUnitSphere);
                WorldRotation = new CanBeDirty<Quaternion>(UnityEngine.Random.rotation);
                PointerIsLocked = new CanBeDirty<bool>(UnityEngine.Random.value > 0.5f);
                CameraType = CanBeDirty.FromEnum(CAMERA_TYPES[UnityEngine.Random.Range(0, CAMERA_TYPES.Count)]);
            }
        }
    }
}
