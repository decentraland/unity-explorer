using Cinemachine;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineThirdPersonCameraData : ICinemachineThirdPersonCameraData
    {
        [SerializeField]
        private CinemachineFreeLook.Orbit[] zoomInOrbitThreshold = new CinemachineFreeLook.Orbit[3];

        [SerializeField]
        private CinemachineFreeLook.Orbit[] zoomOutOrbitThreshold = new CinemachineFreeLook.Orbit[3];

        [field: SerializeField]
        public float ZoomSensitivity { get; private set; } = 0.05f;

        public IReadOnlyList<CinemachineFreeLook.Orbit> ZoomInOrbitThreshold => zoomInOrbitThreshold;

        public IReadOnlyList<CinemachineFreeLook.Orbit> ZoomOutOrbitThreshold => zoomOutOrbitThreshold;

        [field: SerializeField]
        public CinemachineFreeLook Camera { get; private set; }

        [field: SerializeField] public CinemachineCameraOffset CameraOffset { get; private set; }

        [field: SerializeField] public Vector3 OffsetBottom { get; private set; }
        [field: SerializeField] public Vector3 OffsetMid { get; private set; }
        [field: SerializeField] public Vector3 OffsetTop { get; private set; }
    }
}
