using Cinemachine;
using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineFirstPersonCameraData : ICinemachineFirstPersonCameraData
    {
        private CinemachinePOV cachedPOV;

        [field: SerializeField]
        public CinemachineVirtualCamera Camera { get; private set; }

        public CinemachinePOV POV => cachedPOV ??= Camera.GetCinemachineComponent<CinemachinePOV>();
    }
}
