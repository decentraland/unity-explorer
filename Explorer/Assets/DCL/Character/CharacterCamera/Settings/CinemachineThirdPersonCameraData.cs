using Cinemachine;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineThirdPersonCameraData2 : ICinemachineThirdPersonCameraData2
    {
        [field: SerializeField] public CinemachineVirtualCamera Camera { get; private set; }

        [field: SerializeField] public Vector3 OffsetBottom { get; private set; }
        [field: SerializeField] public Vector3 OffsetMid { get; private set; }
        [field: SerializeField] public Vector3 OffsetTop { get; private set; }

        private CinemachineBasicMultiChannelPerlin cachedNoise;
        public CinemachineBasicMultiChannelPerlin Noise => cachedNoise ??= Camera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }
}
