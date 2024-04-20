using Cinemachine;
using DCL.CharacterCamera.Components;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    public class CinemachinePreset : MonoBehaviour, ICinemachinePreset
    {
        [SerializeField] private CinemachineFirstPersonCameraData firstPersonCameraData;
        [SerializeField] private CinemachineThirdPersonCameraData thirdPersonCameraData;
        [SerializeField] private CinemachineFreeCameraData freeCameraData;


        [field: SerializeField]
        public CameraMode DefaultCameraMode { get; private set; }

        [field: SerializeField]
        public CinemachineBrain Brain { get; private set; }

        ICinemachineThirdPersonCameraData ICinemachinePreset.ThirdPersonCameraData => thirdPersonCameraData;

        ICinemachineFirstPersonCameraData ICinemachinePreset.FirstPersonCameraData => firstPersonCameraData;

        ICinemachineFreeCameraData ICinemachinePreset.FreeCameraData => freeCameraData;
    }
}
