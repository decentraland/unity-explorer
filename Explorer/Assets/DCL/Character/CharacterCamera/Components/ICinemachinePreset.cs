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

        internal ICinemachineThirdPersonCameraData ThirdPersonCameraData { get; }
        internal ICinemachineThirdPersonCameraData DroneViewCameraData { get; }
        internal ICinemachineFirstPersonCameraData FirstPersonCameraData { get; }

        internal ICinemachineFreeCameraData FreeCameraData { get; }
    }
}
