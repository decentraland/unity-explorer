using Cinemachine;
using DCL.CharacterCamera.Settings;

namespace DCL.CharacterCamera.Components
{
    /// <summary>
    ///     Data for camera setup based on the Unity's Cinemachine
    /// </summary>
    public interface ICinemachinePreset
    {
        CameraMode DefaultCameraMode { get; }

        CinemachineBrain Brain { get; }

        /// <summary>
        ///     Absolute mouse wheel threshold to change the camera mode
        /// </summary>
        float CameraModeMouseWheelThreshold { get; }

        internal ICinemachineThirdPersonCameraData ThirdPersonCameraData { get; }

        internal ICinemachineFirstPersonCameraData FirstPersonCameraData { get; }

        internal ICinemachineFreeCameraData FreeCameraData { get; }
    }
}
