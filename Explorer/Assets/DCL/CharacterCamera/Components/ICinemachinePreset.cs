using Cinemachine;
using DCL.CharacterCamera.Settings;

namespace DCL.CharacterCamera.Components
{
    /// <summary>
    ///     Data for camera setup based on the Unity's Cinemachine
    /// </summary>
    internal interface ICinemachinePreset
    {
        CameraMode DefaultCameraMode { get; }

        CinemachineBrain Brain { get; }

        /// <summary>
        ///     Absolute mouse wheel threshold to change the camera mode
        /// </summary>
        float CameraModeMouseWheelThreshold { get; }

        ICinemachineThirdPersonCameraData ThirdPersonCameraData { get; }

        ICinemachineFirstPersonCameraData FirstPersonCameraData { get; }

        ICinemachineFreeCameraData FreeCameraData { get; }
    }
}
