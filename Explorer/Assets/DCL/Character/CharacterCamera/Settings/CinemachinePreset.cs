using Cinemachine;
using DCL.CharacterCamera.Components;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    public class CinemachinePreset : MonoBehaviour, ICinemachinePreset
    {
        [SerializeField] private CinemachineFirstPersonCameraData firstPersonCameraData = null!;
        [SerializeField] private CinemachineThirdPersonCameraData thirdPersonCameraData = null!;
        [SerializeField] private CinemachineThirdPersonCameraData droneViewCameraData = null!;
        [SerializeField] private CinemachineFreeCameraData freeCameraData = null!;
        [SerializeField] private  CinemachineFreeCameraData inWorldCameraData = null!;

        [SerializeField] private int shoulderChangeSpeed;

        private void Awake()
        {
            firstPersonCameraData.Camera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
            thirdPersonCameraData.Camera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
            droneViewCameraData.Camera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
            freeCameraData.Camera.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Never;
        }

        [field: SerializeField]
        public CameraMode DefaultCameraMode { get; private set; }

        [field: SerializeField]
        public CinemachineBrain Brain { get; private set; } = null!;

        ICinemachineThirdPersonCameraData ICinemachinePreset.ThirdPersonCameraData => thirdPersonCameraData;

        ICinemachineFirstPersonCameraData ICinemachinePreset.FirstPersonCameraData => firstPersonCameraData;
        ICinemachineThirdPersonCameraData ICinemachinePreset.DroneViewCameraData => droneViewCameraData;

        ICinemachineFreeCameraData ICinemachinePreset.FreeCameraData => freeCameraData;

        ICinemachineFreeCameraData ICinemachinePreset.InWorldCameraData => inWorldCameraData;

        int ICinemachinePreset.ShoulderChangeSpeed => shoulderChangeSpeed;
    }
}
