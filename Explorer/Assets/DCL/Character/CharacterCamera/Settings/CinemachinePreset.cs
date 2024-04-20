using Cinemachine;
using DCL.Audio;
using DCL.CharacterCamera.Components;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    public class CinemachinePreset : MonoBehaviour, ICinemachinePreset
    {
        [SerializeField] private CinemachineFirstPersonCameraData firstPersonCameraData;
        [SerializeField] private CinemachineThirdPersonCameraData thirdPersonCameraData;
        [SerializeField] private CinemachineFreeCameraData freeCameraData;

        [Header("Audio")]
        [SerializeField] private AudioClipConfig zoomInAudio;
        [SerializeField] private AudioClipConfig zoomOutAudio;

        [field: SerializeField]
        public CameraMode DefaultCameraMode { get; private set; }

        [field: SerializeField]
        public CinemachineBrain Brain { get; private set; }

        ICinemachineThirdPersonCameraData ICinemachinePreset.ThirdPersonCameraData => thirdPersonCameraData;

        ICinemachineFirstPersonCameraData ICinemachinePreset.FirstPersonCameraData => firstPersonCameraData;

        ICinemachineFreeCameraData ICinemachinePreset.FreeCameraData => freeCameraData;

        AudioClipConfig ICinemachinePreset.ZoomInAudio => zoomInAudio;

        AudioClipConfig ICinemachinePreset.ZoomOutAudio => zoomOutAudio;
    }
}
