using Arch.Core;
using Cinemachine;
using DCL.Utilities;
using System.Collections.Generic;
using UnityEngine;
using Utility;
using CameraType = DCL.ECSComponents.CameraType;

namespace DCL.CharacterCamera
{
    public class ExposedCameraData : IExposedCameraData
    {
        private static readonly IEqualityComparer<Quaternion> EQUALITY_COMPARER_WITH_ERROR =
            QuaternionUtils.CreateEqualityComparer(QuaternionUtils.DEFAULT_ERROR * 100);

        public CanBeDirty<Vector3> WorldPosition;
        public CanBeDirty<Quaternion> WorldRotation = new (Quaternion.identity, EQUALITY_COMPARER_WITH_ERROR);
        public CanBeDirty<bool> PointerIsLocked;
        public CanBeDirty<CameraType> CameraType = CanBeDirty.FromEnum<CameraType>();
        public ObjectProxy<Entity> CameraEntityProxy { get; } = new ();
        public CinemachineBrain? CinemachineBrain { get; set; }

        CanBeDirty<Vector3> IExposedCameraData.WorldPosition => WorldPosition;
        CanBeDirty<Quaternion> IExposedCameraData.WorldRotation => WorldRotation;
        CanBeDirty<CameraType> IExposedCameraData.CameraType => CameraType;
        CanBeDirty<bool> IExposedCameraData.PointerIsLocked => PointerIsLocked;
    }
}
