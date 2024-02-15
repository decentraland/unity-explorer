using UnityEngine;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     A special marker of the camera entity, a generic way of its representation disconnected from the implementation.
    ///     it is the only exposed component to other assemblies
    /// </summary>
    public struct CameraComponent
    {
        public CameraMode Mode;
        public bool LockCameraInput;
        public readonly Camera Camera;

        public CameraComponent(Camera camera) : this()
        {
            Camera = camera;
            LockCameraInput = false;
        }
    }
}
