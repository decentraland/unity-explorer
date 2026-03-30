using DCL.Audio;
using DCL.Prefs;
using System;

namespace DCL.AuthenticationScreenFlow
{
    public class AuthenticationScreenAudio : IDisposable
    {
        private readonly AuthenticationScreenView? viewInstance;
        private readonly AudioMixerVolumesController audioMixerVolumesController;

        private readonly AudioClipConfig backgroundMusic;

        public AuthenticationScreenAudio(AuthenticationScreenView? viewInstance,
            AudioMixerVolumesController audioMixerVolumesController,
            AudioClipConfig backgroundMusic)
        {
            this.viewInstance = viewInstance;
            this.audioMixerVolumesController = audioMixerVolumesController;
            this.backgroundMusic = backgroundMusic;
        }

        public void Dispose()
        {
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
        }

        public void OnShow()
        {
            viewInstance.MuteButton.Button.onClick.AddListener(OnMuteButtonClicked);

            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.MuteGroup(AudioMixerExposedParam.Chat_Volume);

            // Unregistering in case player re-login midgame.
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent += OnContinuousAudioStarted;

            InitMusicMute();
        }

        public void OnHide()
        {
            viewInstance.MuteButton.Button.onClick.RemoveListener(OnMuteButtonClicked);

            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.World_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Avatar_Volume);
            audioMixerVolumesController.UnmuteGroup(AudioMixerExposedParam.Chat_Volume);
        }

        private void OnMuteButtonClicked()
        {
            bool isMuted = DCLPlayerPrefs.GetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, false);

            UIAudioEventsBus.Instance.SendMuteContinuousAudioEvent(backgroundMusic, !isMuted);

            viewInstance?.MuteButton.SetIcon(!isMuted);

            DCLPlayerPrefs.SetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, !isMuted, save: true);
        }

        private void OnContinuousAudioStarted(AudioClipConfig audioClipConfig)
        {
            if (audioClipConfig.GetInstanceID() != backgroundMusic.GetInstanceID())
                return;

            UIAudioEventsBus.Instance.PlayContinuousUIAudioEvent -= OnContinuousAudioStarted;
            InitMusicMute();
        }

        private void InitMusicMute()
        {
            bool isMuted = DCLPlayerPrefs.GetBool(DCLPrefKeys.AUTHENTICATION_SCREEN_MUSIC_MUTED, false);

            if (isMuted)
                UIAudioEventsBus.Instance.SendMuteContinuousAudioEvent(backgroundMusic, true);

            viewInstance?.MuteButton.SetIcon(isMuted);
        }
    }
}
