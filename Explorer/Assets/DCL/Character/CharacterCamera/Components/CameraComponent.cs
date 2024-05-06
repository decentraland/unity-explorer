using System.Collections.Generic;
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
        public ThirdPersonCameraShoulder Shoulder;
        public readonly Camera Camera;
        private readonly HashSet<object> cameraInputLocks;

        public CameraComponent(Camera camera) : this()
        {
            Camera = camera;
            cameraInputLocks = new HashSet<object>();
        }

        public Transform PlayerFocus { get; set; }

        public bool CameraInputChangeEnabled => GetInputLocks().Count == 0;

        private HashSet<object> GetInputLocks() =>
            cameraInputLocks ?? new HashSet<object>();

        public void AddCameraInputLock(object context)
        {
            cameraInputLocks.Add(context);
        }

        public void RemoveCameraInputLock(object context)
        {
            cameraInputLocks.Remove(context);
        }
    }
}
