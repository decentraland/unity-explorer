using DCL.Audio;
using DCL.Prefs;
using DCL.Settings.ModuleViews;
using DCL.Settings.Utils;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
	public class MusicVolumeSettingsController : SettingsFeatureController
	{
		private const string MUSIC_VOLUME_EXPOSED_PARAM = "Music_Volume";

		private readonly SettingsSliderModuleView view;
		private readonly AudioMixer generalAudioMixer;
		private readonly VolumeBus volumeBus;
		
		public MusicVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
		{
			this.view = view;
			this.generalAudioMixer = generalAudioMixer;
			this.volumeBus = volumeBus;
			
			if (DCLPlayerPrefs.GetBool(DCLPrefKeys.SETTINGS_MUSIC_AND_SFX_MUTED))
			{
				view.SliderView.Slider.value = 0;
				SetMusicVolumeSettingsWithoutSerialization(0);
			}
			else if (DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MUSIC_VOLUME))
			{
				view.SliderView.Slider.value = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MUSIC_VOLUME);
				SetMusicVolumeSettings(view.SliderView.Slider.value);
			}
			else
			{
				view.SliderView.Slider.value = 1;
				SetMusicVolumeSettings(1);
			}
            
			view.SliderView.Slider.onValueChanged.AddListener(SetMusicVolumeSettings);
			volumeBus.OnMusicAndSFXMuteChanged += SettingsMuteChanged;
		}
		
		private void SetMusicVolumeSettings(float volumePercentage)
		{
			generalAudioMixer.SetFloat(MUSIC_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
			DCLPlayerPrefs.SetFloat(DCLPrefKeys.SETTINGS_MUSIC_VOLUME, volumePercentage, save: true);

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMusicVolume(volumePercentage / 100);
#endif
		}
        
		private void SetMusicVolumeSettingsWithoutSerialization(float volumePercentage)
		{
			generalAudioMixer.SetFloat(MUSIC_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMusicVolume(volumePercentage / 100);
#endif
		}

		private void SettingsMuteChanged(bool value)
		{
			float volumePercentage = 0;
			if (value || !DCLPlayerPrefs.HasKey(DCLPrefKeys.SETTINGS_MUSIC_VOLUME))
				view.SliderView.Slider.SetValueWithoutNotify(0);
			else
			{
				volumePercentage = DCLPlayerPrefs.GetFloat(DCLPrefKeys.SETTINGS_MUSIC_VOLUME);
				view.SliderView.Slider.SetValueWithoutNotify(volumePercentage);
			}
            
			generalAudioMixer.SetFloat(MUSIC_VOLUME_EXPOSED_PARAM,  AudioUtils.PercentageVolumeToDecibel(volumePercentage));
            
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            volumeBus.SetMusicVolume(volumePercentage / 100);
#endif
		}
		
		public override void Dispose()
		{
			view.SliderView.Slider.onValueChanged.RemoveListener(SetMusicVolumeSettings);
			volumeBus.OnMusicAndSFXMuteChanged -= SettingsMuteChanged;
		}
	}
}