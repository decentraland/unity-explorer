using DCL.AssetsProvision;
using DCL.Audio;
using DCL.CharacterCamera.Settings;
using System;
using UnityEngine;

namespace DCL.PluginSystem.Global
{
    [Serializable]
    public class CharacterCameraSettings : IDCLPluginSettings
    {
        [field: Header(nameof(CharacterCameraSettings))]
        [field: Space]
        [field: SerializeField] internal CinemachinePresetRef cinemachinePreset { get; private set; }
        [field: SerializeField] internal CinemachineCameraAudioSettingsReference cinemachineCameraAudioSettingsReference { get; private set; }

        [Serializable]
        public class CinemachinePresetRef : ComponentReference<CinemachinePreset>
        {
            public CinemachinePresetRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class CinemachineCameraAudioSettingsReference : ComponentReference<CinemachineCameraAudioSettings>
        {
            public CinemachineCameraAudioSettingsReference(string guid) : base(guid) { }
        }
    }
}
