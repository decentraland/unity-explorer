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
		private readonly MusicVolumeSettingsController musicVolumeSettingsController;
		private readonly WorldSoundsVolumeSettingsController worldSoundsVolumeSettingsController;
		
		public MusicAndSFXVolumeSettingsController(SettingsSliderModuleView view, AudioMixer generalAudioMixer, VolumeBus volumeBus)
		{
			musicVolumeSettingsController = new MusicVolumeSettingsController(view, generalAudioMixer);
			worldSoundsVolumeSettingsController = new WorldSoundsVolumeSettingsController(view, generalAudioMixer, volumeBus);
		}

		public override void Dispose()
		{
			musicVolumeSettingsController.Dispose();
			worldSoundsVolumeSettingsController.Dispose();
		}
	}
}