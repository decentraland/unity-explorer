using Cinemachine;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    internal interface ICinemachineThirdPersonCameraData
    {
        CinemachineFreeLook Camera { get; }
        CinemachineCameraOffset CameraOffset { get; }
        Vector3 OffsetBottom { get; }
        Vector3 OffsetMid { get; }
        Vector3 OffsetTop { get; }
    }

    internal interface ICinemachineThirdPersonCameraData2
    {
        CinemachineVirtualCamera Camera { get; }
        CinemachineCameraOffset CameraOffset { get; }
        Vector3 OffsetBottom { get; }
        Vector3 OffsetMid { get; }
        Vector3 OffsetTop { get; }

        CinemachinePOV POV { get; }

        CinemachineBasicMultiChannelPerlin Noise { get; }
    }
}
