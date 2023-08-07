using Cinemachine;
using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CinemachineFreeCameraData : ICinemachineFreeCameraData
    {
        [field: SerializeField]
        public CinemachineVirtualCamera Camera { get; private set; }
    }
}
