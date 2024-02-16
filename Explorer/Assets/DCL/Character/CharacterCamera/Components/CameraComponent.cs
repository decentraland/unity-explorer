using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     A special marker of the camera entity, a generic way of its representation disconnected from the implementation.
    ///     it is the only exposed component to other assemblies
    /// </summary>
    public struct CameraComponent
    {
        public CameraMode Mode;
        public int CameraInputLocks;
        public readonly Camera Camera;

        public CameraComponent(Camera camera) : this()
        {
            Camera = camera;
            CameraInputLocks = 0;
        }

        public bool CameraInputChangeEnabled => CameraInputLocks == 0;

        public void AddCameraInputLock() =>
            CameraInputLocks++;

        public void RemoveCameraInputLock() =>
            CameraInputLocks = CameraInputLocks - 1 < 0 ? 0 : CameraInputLocks - 1;
    }
}
