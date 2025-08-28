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
        public event Action<float> OnMusicVolumeChanged;
        public event Action<bool> OnGlobalMuteChanged; 
        public event Action<bool> OnMusicAndSFXMuteChanged; 
        
        public float GetSerializedMasterVolume() =>
            DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MASTER_VOLUME, 100f) / 100f;

        public float GetSerializedWorldVolume() =>
            DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_WORLD_VOLUME, 100f) / 100f;

        public float GetSerializedMusicVolume() =>
            DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MUSIC_VOLUME, 100f) / 100f;
        
        public void SetWorldVolume(float volume)
        {
            OnWorldVolumeChanged?.Invoke(volume);
        }

        public void SetMasterVolume(float volume)
        {
            OnMasterVolumeChanged?.Invoke(volume);
        }

        public void SetMusicVolume(float volume)
        {
            OnMusicVolumeChanged?.Invoke(volume);
        }

        public bool GetGlobalMuteValue()
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MASTER_MUTED))
                return DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MASTER_MUTED);

            return false;
        }

        public void SetGlobalMute(bool value)
        {
            if(DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MASTER_MUTED) 
               && DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MASTER_MUTED) == value)
                return;
            
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_MASTER_MUTED, value, save: true);
            
            OnGlobalMuteChanged?.Invoke(value);
        }

        public void SetMusicAndSFXMute(bool value)
        {
            if(DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED) 
               && DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED) == value)
                return;
            
            DCLPlayerPrefs.SetBool(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED, value, save: true);
            
            OnMusicAndSFXMuteChanged?.Invoke(value);
        }

        public bool GetMusicAndSFXMuteValue()
        {
            if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED))
                return DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED);

            return false;
        }
    }
}
