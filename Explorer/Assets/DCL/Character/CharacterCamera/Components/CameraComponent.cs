using UnityEngine;

namespace DCL.CharacterCamera
{
    /// <summary>
    ///     A special marker of the camera entity, a generic way of its representation disconnected from the implementation.
    ///     it is the only exposed component to other assemblies
    /// </summary>
    public struct CameraComponent
    {
        private CameraMode mode;
        public CameraMode Mode
        {
            get => mode;
            set
            {
                if (value == mode) return;

                PreviousMode = mode;
                mode = value;
            }
        }
        public CameraMode PreviousMode { get; private set; }
        public ThirdPersonCameraShoulder Shoulder;
        public readonly Camera Camera;
        public bool IsDirty;

        public CameraComponent(Camera camera) : this()
        {
            Camera = camera;
            CameraInputLocks = 0;
            Mode = CameraMode.FirstPerson;
        }

        public int CameraInputLocks { get; private set; }

        public bool CameraInputChangeEnabled => CameraInputLocks == 0;
        public Transform PlayerFocus { get; set; }

        public void AddCameraInputLock()
        {
            CameraInputLocks++;
            IsDirty = true;
        }

        public void RemoveCameraInputLock()
        {
            CameraInputLocks = CameraInputLocks - 1 < 0 ? 0 : CameraInputLocks - 1;
            IsDirty = true;
        }

    }
}
