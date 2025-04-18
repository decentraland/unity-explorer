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

        internal ICinemachineFirstPersonCameraData FirstPersonCameraData { get; }
        internal ICinemachineThirdPersonCameraData2 ThirdPersonCameraData { get; }
        internal ICinemachineThirdPersonCameraData2 DroneViewCameraData { get; }
        internal ICinemachineFreeCameraData FreeCameraData { get; }
        internal ICinemachineFreeCameraData InWorldCameraData { get; }

        internal int ShoulderChangeSpeed { get; }
    }
}
