using Cinemachine;

namespace DCL.CharacterCamera.Components
{
    /// <summary>
    ///     State of the camera based on the Unity's Cinemachine that changes at runtime
    /// </summary>
    internal struct CinemachineCameraState
    {
        /// <summary>
        ///     Value between [0, 1].
        ///     0 means the camera is in the closest position.
        ///     1 means the camera is in the farthest position.
        ///     When the camera is zoomed in from the closest position, it switches to the first person mode.
        /// </summary>
        public float ThirdPersonZoomValue;

        /// <summary>
        ///     Data of the current camera mode in case access to the base camera is enough
        /// </summary>
        public CinemachineVirtualCameraBase CurrentCamera;
    }
}
