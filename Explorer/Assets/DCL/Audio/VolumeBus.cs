using DCL.Prefs;
using System;

namespace DCL.Audio
{
    public class VolumeBus
    {
        private const string WORLD_VOLUME_DATA_STORE_KEY = "Settings_WorldVolume";
        private const string MASTER_VOLUME_DATA_STORE_KEY = "Settings_MasterVolume";

        public event Action<float> OnWorldVolumeChanged;
        public event Action<float> OnMasterVolumeChanged;
        public event Action<bool> OnGlobalMuteChanged; 

        public void SetWorldVolume(float volume)
        {
            OnWorldVolumeChanged?.Invoke(volume);
        }

        public void SetMasterVolume(float volume)
        {
            OnMasterVolumeChanged?.Invoke(volume);
        }

        public float GetWorldVolume()
        {
            if (DCLPlayerPrefs.HasKey(WORLD_VOLUME_DATA_STORE_KEY))
                return DCLPlayerPrefs.GetFloat(WORLD_VOLUME_DATA_STORE_KEY) / 100;

            return 1f;
        }

        public float GetMasterVolume()
        {
            if (DCLPlayerPrefs.HasKey(MASTER_VOLUME_DATA_STORE_KEY))
                return DCLPlayerPrefs.GetFloat(MASTER_VOLUME_DATA_STORE_KEY) / 100;

            return 1f;
        }

        public bool GetGlobalMuteValue()
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_VOLUME_MUTED))
                return DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_VOLUME_MUTED);

            return false;
        }

        public void SetGlobalMute(bool value)
        {
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_VOLUME_MUTED, value, save: true);
            
            OnGlobalMuteChanged?.Invoke(value);
        }
    }
}
