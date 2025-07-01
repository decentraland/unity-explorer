using System;
using UnityEngine;

namespace DCL.CharacterCamera.Settings
{
    [Serializable]
    public class CameraFarClipPlaneSettings
    {
        [field: SerializeField] public float MinFarClipPlaneAltitude { get; set; } = 0;

        [field: SerializeField] public float MaxFarClipPlaneAltitude { get; set; } = 100;

        [field: SerializeField] public float MinFarClipPlane { get; set; } = 500;

        [field: SerializeField] public float MaxFarClipPlane { get; set; } = 1000;
    }
}
