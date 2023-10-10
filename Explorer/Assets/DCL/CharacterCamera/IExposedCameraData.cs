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
    }
}
