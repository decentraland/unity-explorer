using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.Audio;
using DG.Tweening;


namespace DCL.Audio
{
    public class AudioGeneralController : MonoBehaviour, IDisposable
    {
        private Tweener volumeTweener;

        [SerializeField] private AudioSettings audioSettings;
        [SerializeField] private UIAudioPlaybackController playbackController;

        public void Dispose()
        {
            volumeTweener.Kill();
        }

        public void Initialize()
        {
            playbackController.Initialize(audioSettings);
        }

        public void ChangeVolumeOfCategory(AudioCategory category, float volume)
        {
            var audioMixer = audioSettings.MasterAudioMixer;
            //Not Implemented yet :)
        }

        private void ReduceVolumeOnMenu(bool reduce)
        {

        }

        private AudioMixerGroup GetAudioMixerForCategory(AudioCategory category) =>
            audioSettings.GetSettingsForCategory(category).MixerGroup;
    }
}
