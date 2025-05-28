using Cinemachine;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineThirdPersonCameraData : ICinemachineThirdPersonCameraData
    {
        [field: SerializeField] public CinemachineFreeLook Camera { get; private set; }
        [field: SerializeField] public CinemachineCameraOffset CameraOffset { get; private set; }

        [field: SerializeField] public Vector3 OffsetBottom { get; private set; }
        [field: SerializeField] public Vector3 OffsetMid { get; private set; }
        [field: SerializeField] public Vector3 OffsetTop { get; private set; }
    }
}
