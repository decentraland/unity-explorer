using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CameraFarClipPlaneSettings
    {
        [field: SerializeField] public float MinFarClipPlaneAltitude { get; private set; } = 0;

        [field: SerializeField] public float MaxFarClipPlaneAltitude { get; private set; } = 100;

        [field: SerializeField] public float MinFarClipPlane { get; private set; } = 500;

        [field: SerializeField] public float MaxFarClipPlane { get; private set; } = 1000;
    }
}
