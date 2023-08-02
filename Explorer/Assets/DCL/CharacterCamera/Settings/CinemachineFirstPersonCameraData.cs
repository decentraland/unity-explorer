using Cinemachine;
using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineFirstPersonCameraData : ICinemachineFirstPersonCameraData
    {
        [field: SerializeField]
        public CinemachineVirtualCamera Camera { get; private set; }
    }
}
