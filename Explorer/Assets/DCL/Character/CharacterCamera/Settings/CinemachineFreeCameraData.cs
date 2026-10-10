using Cinemachine;
using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineFreeCameraData : ICinemachineFreeCameraData
    {
        private CinemachinePOV cachedPOV;

        [field: SerializeField]
        public CinemachineVirtualCamera Camera { get; private set; }

        [field: SerializeField]
        public float Speed { get; private set; } = 15f;

        [field: SerializeField]
        public Vector3 DefaultPosition { get; private set; } = Vector3.up * 15;

        public CinemachinePOV POV => cachedPOV ??= Camera.GetCinemachineComponent<CinemachinePOV>();
    }
}
