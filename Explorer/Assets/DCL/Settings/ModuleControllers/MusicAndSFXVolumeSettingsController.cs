using DCL.Audio;
using DCL.Settings.ModuleViews;
using UnityEngine.Audio;

namespace DCL.Settings.ModuleControllers
{
	/// <summary>
	/// Music and SFX settings binder. Binds both to same slider.
	/// </summary>
	public class MusicAndSFXVolumeSettingsController : SettingsFeatureController
	{
		private readonly SettingsSliderModuleView view;
		private readonly VolumeBus volumeBus;
		private readonly MusicVolumeSettingsController musicVolumeSettingsController;
		private readonly WorldSoundsVolumeSettingsController worldSoundsVolumeSettingsController;
		
		public MusicAndSFXVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
		{
			this.view = view;
			this.volumeBus = volumeBus;
			musicVolumeSettingsController = new MusicVolumeSettingsController(view, generalAudioMixer, volumeBus);
			worldSoundsVolumeSettingsController = new WorldSoundsVolumeSettingsController(view, generalAudioMixer, volumeBus);
			
			view.SliderView.Slider.onValueChanged.AddListener(OnSliderValueChanged);
		}

		private void OnSliderValueChanged(float volumePercentage)
		{
			if (volumePercentage > 0)
				volumeBus.SetMusicAndSFXMute(false);
		}

		public override void Dispose()
		{
			musicVolumeSettingsController.Dispose();
			worldSoundsVolumeSettingsController.Dispose();
			
			view.SliderView.Slider.onValueChanged.RemoveListener(OnSliderValueChanged);
		}
	}
}