using DCL.CharacterCamera.Settings;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class CharacterCameraSettings : IDCLPluginSettings
    {
        [Serializable]
        public class CinemachinePresetRef : ComponentReference<CinemachinePreset>
        {
            public CinemachinePresetRef(string guid) : base(guid) { }
        }

        [field: Header(nameof(CharacterCameraSettings))]
        [field: Space]
        [field: SerializeField] internal CinemachinePresetRef cinemachinePreset { get; private set; }
    }
}
